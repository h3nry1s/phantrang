using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace PhanTrang
{
    /// <summary>Appends a line to %APPDATA%\PhanTrang\nhatky.txt (never throws).</summary>
    static class Log
    {
        const long MaxBytes = 1L << 20;

        public static void Write(string text)
        {
            try
            {
                Directory.CreateDirectory(Settings.Dir);
                string path = Path.Combine(Settings.Dir, "nhatky.txt");
                var info = new FileInfo(path);
                if (info.Exists && info.Length > MaxBytes) File.Delete(path);   // keep the log small; old lines are not needed
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + Environment.NewLine);
            }
            catch (Exception) { }
        }
    }

    static class Program
    {
        /// <summary>Signalled by a second copy of the app: show the settings of the one already running.</summary>
        const string ShowSettingsSignal = "PhanTrang-ShowSettings";
        /// <summary>Signalled by the installer or the build: close properly, giving the mouse pointer back.</summary>
        public const string QuitSignal = "PhanTrang-Quit";

        [STAThread]
        static void Main(string[] args)
        {
            bool first;
            using (var single = new Mutex(true, "PhanTrang-SingleInstance", out first))
            {
                if (!first)
                {
                    Signal(ShowSettingsSignal);
                    return;
                }
                Run(args);
                GC.KeepAlive(single);
            }
        }

        static void Signal(string name)
        {
            try { using (var ev = EventWaitHandle.OpenExisting(name)) ev.Set(); }
            catch (WaitHandleCannotBeOpenedException) { }
            catch (UnauthorizedAccessException) { }
        }

        static void Run(string[] args)
        {
            try { Win32.SetProcessDpiAwarenessContext(new IntPtr(-4)); }   // per-monitor v2
            catch (EntryPointNotFoundException) { Win32.SetProcessDPIAware(); }

            BlankCursor.Reset();   // an earlier run may have been killed while the pointer was hidden
            AppDomain.CurrentDomain.UnhandledException += (s, e) => { BlankCursor.Reset(); Log.Write("lỗi nghiêm trọng: " + e.ExceptionObject); };
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ApplicationExit += (s, e) => BlankCursor.Reset();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);   // GDI text: sharp ClearType labels (the bundled fonts are registered with GDI too)
            Fonts.Load();
            Settings.Load();

            var app = new TeacherApp(args);
            Application.ThreadException += (s, e) => { Log.Write("lỗi: " + e.Exception); app.TurnOffSafely(); };

            Listen(ShowSettingsSignal, true, app.ShowSettingsLater);
            Listen(QuitSignal, false, app.QuitLater);

            Application.Run(app);
            BlankCursor.Reset();
        }

        /// <summary>Waits on a named event on a background thread and calls 'then' each time (or once) it is set.</summary>
        static void Listen(string name, bool repeat, Action then)
        {
            var ev = new EventWaitHandle(false, EventResetMode.AutoReset, name);
            var t = new Thread(() =>
            {
                do { ev.WaitOne(); then(); } while (repeat);
            }) { IsBackground = true, Name = name };
            t.Start();
        }
    }
}
