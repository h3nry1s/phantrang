using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PhanTrang
{
    /// <summary>A global shortcut: modifier flags (Win32.Mod) plus a key. A combo without a key means "none".</summary>
    public struct KeyCombo
    {
        public readonly uint Mods;
        public readonly Keys Key;

        public KeyCombo(uint mods, Keys key) { Mods = mods; Key = key; }

        public bool IsNone { get { return Key == Keys.None; } }

        /// <summary>Stored as "mods,virtual-key" (the format earlier versions wrote, so old settings still load).</summary>
        public string Stored { get { return Mods.ToString(CultureInfo.InvariantCulture) + "," + ((uint)Key).ToString(CultureInfo.InvariantCulture); } }

        public static KeyCombo FromStored(string text, KeyCombo fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            int comma = text.IndexOf(',');
            uint mods, key;
            if (comma > 0 && uint.TryParse(text.Substring(0, comma), out mods) && uint.TryParse(text.Substring(comma + 1), out key))
                return new KeyCombo(mods, (Keys)key);
            return fallback;
        }

        /// <summary>How the shortcut reads in Vietnamese UI text, e.g. "Ctrl + Alt + P".</summary>
        public string Display
        {
            get
            {
                if (IsNone) return "Không có";
                var parts = new List<string>();
                if ((Mods & Win32.Mod.Ctrl) != 0) parts.Add("Ctrl");
                if ((Mods & Win32.Mod.Alt) != 0) parts.Add("Alt");
                if ((Mods & Win32.Mod.Shift) != 0) parts.Add("Shift");
                if ((Mods & Win32.Mod.Win) != 0) parts.Add("Win");
                string name = Key.ToString();
                if (Key >= Keys.D0 && Key <= Keys.D9) name = name.Substring(1);   // "D5" -> "5"
                parts.Add(name);
                return string.Join(" + ", parts.ToArray());
            }
        }
    }

    /// <summary>
    /// The teacher's choices, kept in %APPDATA%\PhanTrang\caidat.ini as "name=value" lines. Every change is written
    /// straight away (through a temporary file, so a power cut never leaves a half-written file) and announced
    /// with <see cref="Changed"/>.
    /// </summary>
    public static class Settings
    {
        static readonly Dictionary<string, string> store = new Dictionary<string, string>(StringComparer.Ordinal);
        public static event Action Changed;

        public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhanTrang"); } }
        static string IniPath { get { return Path.Combine(Dir, "caidat.ini"); } }

        public static void Load()
        {
            store.Clear();
            try
            {
                if (!File.Exists(IniPath)) return;
                foreach (var line in File.ReadAllLines(IniPath, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    store[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        static void Write()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string tmp = IniPath + ".tmp";
                File.WriteAllLines(tmp, store.Select(p => p.Key + "=" + p.Value).ToArray(), Encoding.UTF8);
                if (File.Exists(IniPath)) File.Replace(tmp, IniPath, null);
                else File.Move(tmp, IniPath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        static void Put(string key, string value, bool announce = true)
        {
            string old;
            if (store.TryGetValue(key, out old) && old == value) return;
            store[key] = value;
            Write();
            if (announce && Changed != null) Changed();
        }

        static string Text(string key, string fallback) { string v; return store.TryGetValue(key, out v) ? v : fallback; }

        /// <summary>The stored value if it is one of 'allowed', otherwise the fallback.</summary>
        static string OneOf(string key, string fallback, IEnumerable<string> allowed) { string v = Text(key, fallback); return allowed.Contains(v) ? v : fallback; }

        static bool Flag(string key, bool fallback) { string v = Text(key, null); return v == null ? fallback : v == "1"; }
        static void SetFlag(string key, bool value) { Put(key, value ? "1" : "0"); }

        static double Number(string key, double fallback, double min, double max)
        {
            double d;
            if (!double.TryParse(Text(key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out d) || double.IsNaN(d)) return fallback;
            return Math.Max(min, Math.Min(max, d));
        }
        static void SetNumber(string key, double value) { Put(key, value.ToString(CultureInfo.InvariantCulture)); }

        // ---- pointer & tools ----

        /// <summary>chalk | ruler | pen | laser</summary>
        public static string Pointer { get { return OneOf("pointer", "chalk", Catalog.Pointers.Select(p => p[0])); } set { Put("pointer", value); } }
        public static double PointerSize { get { return Number("pointerSize", 0.13, 0.08, 0.22); } set { SetNumber("pointerSize", value); } }
        public static bool Shadow { get { return Flag("shadow", true); } set { SetFlag("shadow", value); } }
        /// <summary>mouse | pointer | type | shape | stamp | draw | vanish | hl | eraser | image</summary>
        public static string Tool { get { return Text("tool", "pointer"); } set { Put("tool", value); } }
        /// <summary>The last tool used in the "Viết" mode, picked up again when switching back to it.</summary>
        public static string WriteTool { get { return Text("writeTool", "type"); } set { Put("writeTool", value); } }
        public static string ShapeKind { get { return Text("shape", "arrow"); } set { Put("shape", value); } }
        public static string StampKind { get { return Text("stamp", "diem10"); } set { Put("stamp", value); } }
        public static string PenWidth { get { return Text("penWidth", "medium"); } set { Put("penWidth", value); } }
        public static string InkChoice { get { return Text("ink", "red"); } set { Put("ink", value); } }
        public static string BoardKind { get { return Text("board", "green"); } set { Put("board", value); } }
        public static string TextFont { get { return Text("textFont", "mali"); } set { Put("textFont", value); } }
        public static string TextSize { get { return Text("textSize", "m"); } set { Put("textSize", value); } }

        /// <summary>The stamps shown in "Chấm bài", in catalogue order; all of them if none is chosen.</summary>
        public static List<string> Stamps
        {
            get
            {
                var chosen = Text("stamps", "").Split(',').Select(s => s.Trim()).ToList();
                var list = Catalog.Stamps.Select(s => s[0]).Where(chosen.Contains).ToList();
                return list.Count > 0 ? list : Catalog.Stamps.Select(s => s[0]).ToList();
            }
            set { Put("stamps", string.Join(",", value.ToArray())); }
        }

        // ---- timing ----

        /// <summary>Seconds before ink on the screen fades; 0 = it stays until cleared.</summary>
        public static double FadeSeconds { get { return Number("fade", 0, 0, 600); } set { SetNumber("fade", value); } }
        /// <summary>Seconds before a "Vẽ tạm" line disappears.</summary>
        public static double VanishSeconds { get { return Number("vanish", 2, 0.5, 60); } set { SetNumber("vanish", value); } }

        // ---- look & behaviour ----

        /// <summary>The round quick-start button while Phấn Trắng is off.</summary>
        public static bool QuickButton { get { return Flag("quickButton", true); } set { SetFlag("quickButton", value); } }
        public static bool Sound { get { return Flag("sound", true); } set { SetFlag("sound", value); } }
        public static bool ToolbarAlways { get { return Flag("toolbarAlways", true); } set { SetFlag("toolbarAlways", value); } }
        public static bool ToolbarLabels { get { return Flag("toolbarLabels", true); } set { SetFlag("toolbarLabels", value); } }
        /// <summary>A "Thoát" button at the end of the toolbar that closes Phấn Trắng completely (tray icon too).</summary>
        public static bool WelcomeShown { get { return Flag("welcomeShown", false); } set { SetFlag("welcomeShown", value); } }

        /// <summary>The key held to click through to the slide: alt | ctrl | shift | off</summary>
        public static string InteractKey { get { return OneOf("interactKey", "alt", new[] { "alt", "ctrl", "shift", "off" }); } set { Put("interactKey", value); } }
        public static bool InteractKeepsPointer { get { return Flag("interactKeeps", false); } set { SetFlag("interactKeeps", value); } }

        public static int InteractVk
        {
            get
            {
                string k = InteractKey;
                return k == "off" ? 0 : k == "ctrl" ? Win32.Vk.Ctrl : k == "shift" ? Win32.Vk.Shift : Win32.Vk.Alt;
            }
        }

        /// <summary>"Alt", "Ctrl", "Shift", or null when click-through is off.</summary>
        public static string InteractName
        {
            get
            {
                string k = InteractKey;
                return k == "off" ? null : char.ToUpperInvariant(k[0]) + k.Substring(1);
            }
        }

        /// <summary>How many times the toolbar tip for a tool was shown (they stop after a few). Not announced: nothing to redraw.</summary>
        public static int HintSeen(string tool) { int n; return int.TryParse(Text("hint." + tool, "0"), out n) ? n : 0; }
        public static void MarkHint(string tool) { Put("hint." + tool, (HintSeen(tool) + 1).ToString(CultureInfo.InvariantCulture), false); }

        // ---- shortcuts ----

        public static KeyCombo ToggleKey { get { return KeyCombo.FromStored(Text("key.toggle", null), new KeyCombo(Win32.Mod.Ctrl | Win32.Mod.Alt, Keys.P)); } set { Put("key.toggle", value.Stored); } }
        public static KeyCombo BoardKey { get { return KeyCombo.FromStored(Text("key.board", null), new KeyCombo(Win32.Mod.Ctrl | Win32.Mod.Alt, Keys.B)); } set { Put("key.board", value.Stored); } }
        public static KeyCombo ClearKey { get { return KeyCombo.FromStored(Text("key.clear", null), new KeyCombo(Win32.Mod.Ctrl | Win32.Mod.Alt, Keys.X)); } set { Put("key.clear", value.Stored); } }

        // ---- exports ----

        /// <summary>The folder of the last PDF / picture export (empty = Documents). Not announced.</summary>
        public static string ExportFolder { get { return Text("exportDir", ""); } set { Put("exportDir", value ?? "", false); } }

        // ---- toolbar and typing panel: size and place ----

        /// <summary>Toolbar size: s | m | l | xl.</summary>
        public static string ToolbarSize { get { return OneOf("toolbarSize", "m", new[] { "s", "m", "l", "xl" }); } set { Put("toolbarSize", value); } }
        public static double ToolbarSizeFactor
        {
            get { switch (ToolbarSize) { case "s": return 0.8; case "l": return 1.25; case "xl": return 1.5; default: return 1; } }
        }

        /// <summary>How visible the toolbar stays while the teacher draws: 0 = hidden, up to 0.8.</summary>
        public static double ToolbarDim { get { return Number("toolbarDim", 0.3, 0, 0.8); } set { SetNumber("toolbarDim", value); } }

        /// <summary>
        /// Where the teacher dragged the toolbar, as a shift from its usual place (bottom centre) in fractions of the
        /// work area, so it lands in the same spot on another screen. Written without announcing: nothing to rebuild.
        /// </summary>
        public static double ToolbarShiftX { get { return Number("toolbarX", 0, -1, 1); } set { Put("toolbarX", value.ToString("0.####", CultureInfo.InvariantCulture), false); } }
        public static double ToolbarShiftY { get { return Number("toolbarY", 0, -1, 0); } set { Put("toolbarY", value.ToString("0.####", CultureInfo.InvariantCulture), false); } }

        /// <summary>The typing panel's place (fraction of the work area for its top left corner; negative = automatic) and size.</summary>
        public static double TypePanelX { get { return Number("typeX", -1, -1, 1); } set { Put("typeX", value.ToString("0.####", CultureInfo.InvariantCulture), false); } }
        public static double TypePanelY { get { return Number("typeY", -1, -1, 1); } set { Put("typeY", value.ToString("0.####", CultureInfo.InvariantCulture), false); } }
        public static double TypePanelSize { get { return Number("typeSize", 1, 0.6, 1.5); } set { Put("typeSize", value.ToString("0.###", CultureInfo.InvariantCulture), false); } }

        // ---- anonymous usage counts (see Usage) ----

        /// <summary>Send anonymous usage counts. On by default; switching it off also forgets the random id.</summary>
        public static bool UsageStats
        {
            get { return Flag("usageStats", true); }
            set { SetFlag("usageStats", value); if (!value) Usage.Forget(); }
        }

        /// <summary>A random id made on this computer the first time counts are sent; tells computers apart, nothing more.</summary>
        public static string DeviceId { get { return Text("deviceId", ""); } set { Put("deviceId", value ?? "", false); } }

        public static string EnsureDeviceId()
        {
            string id = DeviceId;
            if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[0-9a-f]{32}$")) { id = Guid.NewGuid().ToString("N"); DeviceId = id; }
            return id;
        }

        // ---- classroom tools ----

        public static int TimerSeconds { get { return (int)Number("timerSeconds", 300, 1, 5999); } set { Put("timerSeconds", value.ToString(CultureInfo.InvariantCulture), false); } }
        public static int GroupCount { get { return (int)Number("groupCount", 4, 2, 12); } set { Put("groupCount", value.ToString(CultureInfo.InvariantCulture), false); } }
        public static int TeamCount { get { return (int)Number("teamCount", 4, 2, 8); } set { Put("teamCount", value.ToString(CultureInfo.InvariantCulture), false); } }

        /// <summary>Faint ruled lines across the screen while typing, and the next line snaps to them.</summary>
        public static bool TypeLines { get { return Flag("typeLines", true); } set { SetFlag("typeLines", value); } }

        public static void ResetPlaces()
        {
            foreach (var key in new[] { "toolbarX", "toolbarY", "typeX", "typeY", "typeSize" }) store.Remove(key);
            Write();
            if (Changed != null) Changed();
        }

        /// <summary>Back to the defaults. Start-with-Windows (kept by Windows) and the first-run flag stay as they are.</summary>
        public static void ResetAll()
        {
            string welcome;
            bool hadWelcome = store.TryGetValue("welcomeShown", out welcome);
            store.Clear();
            if (hadWelcome) store["welcomeShown"] = welcome;
            Write();
            if (Changed != null) Changed();
        }

        // ---- installed from the Microsoft Store (an MSIX package)? ----

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int GetCurrentPackageFullName(ref int length, StringBuilder name);

        static bool? packaged;

        /// <summary>
        /// True when running from an MSIX package. Such an app cannot add itself to HKCU\...\Run; it starts with Windows
        /// through the startup task in its manifest, which the teacher switches on in Windows Settings.
        /// </summary>
        public static bool Packaged
        {
            get
            {
                if (packaged == null)
                {
                    int length = 0;
                    try { packaged = GetCurrentPackageFullName(ref length, null) != 15700; }   // 15700 = APPMODEL_ERROR_NO_PACKAGE
                    catch (EntryPointNotFoundException) { packaged = false; }               // before Windows 8
                }
                return packaged.Value;
            }
        }

        // ---- start with Windows (a value under HKCU\...\Run; packaged copies use their startup task instead) ----

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", RunName = "PhanTrang";

        public static bool LaunchAtLogin
        {
            get
            {
                if (Packaged) return false;
                try { using (var run = Registry.CurrentUser.OpenSubKey(RunKey)) return run != null && run.GetValue(RunName) != null; }
                catch (System.Security.SecurityException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
            set
            {
                if (Packaged) return;
                try
                {
                    using (var run = Registry.CurrentUser.CreateSubKey(RunKey))
                    {
                        if (value) run.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                        else run.DeleteValue(RunName, false);
                    }
                }
                catch (System.Security.SecurityException) { }
                catch (UnauthorizedAccessException) { }
                if (Changed != null) Changed();
            }
        }
    }
}
