using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace PhanTrang
{
    /// <summary>
    /// Anonymous usage counts, to see how many computers use Phấn Trắng and which tools are worth improving.
    /// What is sent (only while "Gửi số liệu sử dụng ẩn danh" is on, and only when there is Internet):
    ///   a random id made on this computer (not tied to the teacher, the computer's name or the account), the app
    ///   version, Store or download copy, the Windows version, and how many times each tool was used ("tool.draw": 5).
    /// Never sent: names, class lists, anything written or typed, file names or paths.
    /// Sending happens in the background; without a network nothing changes and nothing is retried later.
    /// </summary>
    public static class Usage
    {
        public const string Endpoint = "https://phantrang.org/api/u";
        public const int MaxKeys = 60, MaxCount = 100000;
        static readonly Regex KeyRule = new Regex(@"^[a-z0-9][a-z0-9.\-]{0,39}$");
        static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        static readonly object gate = new object();
        static Timer timer;
        static int sending;
        // read on the UI thread only (the settings store is not thread-safe); the timer thread sees these copies
        static volatile bool enabled;
        static volatile string id;

        static void Refresh()
        {
            enabled = Settings.UsageStats;
            id = enabled ? Settings.EnsureDeviceId() : null;
        }

        /// <summary>Counts one use of a feature (a fixed name such as "tool.draw" or "export.pdf"; never user text).</summary>
        public static void Count(string key)
        {
            if (!enabled || key == null || !KeyRule.IsMatch(key)) return;
            lock (gate)
            {
                int n;
                counts.TryGetValue(key, out n);
                if (n == 0 && counts.Count >= MaxKeys) return;
                counts[key] = Math.Min(MaxCount, n + 1);
            }
        }

        /// <summary>At start-up: counts the start, sends a first time after a short while, then every half hour.</summary>
        public static void Start()
        {
            Refresh();
            Settings.Changed += Refresh;
            Count("start");
            if (timer == null) timer = new Timer(_ => Send(false), null, TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(30));
        }

        /// <summary>When the teacher switches the counting off: what was counted is dropped and the id forgotten.</summary>
        public static void Forget()
        {
            lock (gate) counts.Clear();
            enabled = false;
            id = null;
            Settings.DeviceId = "";
        }

        /// <summary>On quitting: one last short try (at most about two seconds).</summary>
        public static void Flush() { Send(true); }

        static string Channel { get { return Settings.Packaged ? "store" : "tai-ve"; } }

        static string Version
        {
            get { var v = typeof(Usage).Assembly.GetName().Version; return v.Major + "." + v.Minor + "." + v.Build; }
        }

        static void Send(bool quitting)
        {
            string who = id;
            if (!enabled || who == null) return;
            if (Interlocked.Exchange(ref sending, 1) == 1) return;
            Dictionary<string, int> batch;
            lock (gate)
            {
                if (counts.Count == 0) { sending = 0; return; }
                batch = new Dictionary<string, int>(counts);
                counts.Clear();
            }
            string body = Payload(who, Version, Channel, Environment.OSVersion.Version.ToString(), batch);
            Action post = () =>
            {
                bool ok = false;
                try { ok = Post(body, quitting ? 2000 : 8000); }
                catch (Exception e) { Log.Write("gửi số liệu sử dụng: " + e.GetType().Name); }
                if (!ok && !quitting) lock (gate) foreach (var kv in batch) { int n; counts.TryGetValue(kv.Key, out n); counts[kv.Key] = Math.Min(MaxCount, n + kv.Value); }
                Interlocked.Exchange(ref sending, 0);
            };
            if (quitting) post();
            else new Thread(() => post()) { IsBackground = true, Name = "PhanTrang usage" }.Start();
        }

        static bool Post(string body, int timeoutMs)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;   // TLS 1.2 on .NET 4.5+
            var req = (HttpWebRequest)WebRequest.Create(Endpoint);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.UserAgent = "PhanTrang/" + Version;
            req.Timeout = req.ReadWriteTimeout = timeoutMs;
            var bytes = Encoding.UTF8.GetBytes(body);
            req.ContentLength = bytes.Length;
            using (var s = req.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
            using (var res = (HttpWebResponse)req.GetResponse()) return (int)res.StatusCode < 300;
        }

        /// <summary>
        /// The JSON sent: {"id":"…","v":"1.3.0","ch":"tai-ve","os":"10.0.26200.0","n":{"start":1,"tool.draw":5}}.
        /// Only well-formed keys and sensible numbers get in. Public for the self-tests.
        /// </summary>
        public static string Payload(string id, string version, string channel, string os, IDictionary<string, int> batch)
        {
            var sb = new StringBuilder("{");
            sb.Append("\"id\":\"").Append(Safe(id, 36)).Append("\",");
            sb.Append("\"v\":\"").Append(Safe(version, 20)).Append("\",");
            sb.Append("\"ch\":\"").Append(Safe(channel, 10)).Append("\",");
            sb.Append("\"os\":\"").Append(Safe(os, 24)).Append("\",\"n\":{");
            bool first = true;
            foreach (var kv in batch.Where(kv => kv.Key != null && KeyRule.IsMatch(kv.Key) && kv.Value > 0).OrderBy(kv => kv.Key, StringComparer.Ordinal).Take(MaxKeys))
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(kv.Key).Append("\":").Append(Math.Min(MaxCount, kv.Value).ToString(CultureInfo.InvariantCulture));
            }
            return sb.Append("}}").ToString();
        }

        /// <summary>Letters, digits, dot and dash only: nothing that could carry anything else.</summary>
        static string Safe(string s, int max)
        {
            var t = Regex.Replace(s ?? "", @"[^A-Za-z0-9.\-]", "");
            return t.Length > max ? t.Substring(0, max) : t;
        }
    }
}
