// PhanTrang-Setup.exe: a small per-user installer (no administrator rights, no UAC prompt).
//   PhanTrang-Setup.exe              window with "Cài đặt"
//   PhanTrang-Setup.exe /S           silent install / upgrade
//   Uninstall.exe /uninstall         (copied into the install folder) removes the app, keeps the teacher's lessons
// The app files travel inside as an embedded zip ("PhanTrang.payload.zip") with a "PhanTrang.version.txt".
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Cài đặt Phấn Trắng")]
[assembly: AssemblyDescription("Trình cài đặt Phấn Trắng cho Windows")]
[assembly: AssemblyCompany("Viên Thế Hưng")]
[assembly: AssemblyProduct("Phấn Trắng")]
[assembly: AssemblyCopyright("© 2026 Viên Thế Hưng")]
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]

static class Setup
{
    const string AppName = "Phấn Trắng";
    const string KeyName = "PhanTrang";
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\PhanTrang";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    static string InstallDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PhanTrang"); } }
    static string AppExe { get { return Path.Combine(InstallDir, "PhanTrang.exe"); } }
    static string StartMenuLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk"); } }
    static string DesktopLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk"); } }

    [STAThread]
    static int Main(string[] args)
    {
        try { SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        bool silent = args.Any(a => a.Equals("/S", StringComparison.OrdinalIgnoreCase));
        if (args.Any(a => a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase))) return Uninstall(silent);
        if (silent)
        {
            try { Install(true, false, false, null); return 0; }
            catch (Exception e) { File.AppendAllText(Path.Combine(Path.GetTempPath(), "phantrang-setup.log"), DateTime.Now + " " + e + Environment.NewLine); return 1; }
        }
        Application.Run(new SetupForm());
        return 0;
    }

    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint a, uint b, IntPtr c, uint d);

    public static string Version
    {
        get
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("PhanTrang.version.txt"))
            using (var r = new StreamReader(s, Encoding.UTF8)) return r.ReadToEnd().Trim();
        }
    }

    public static bool IsInstalled { get { return File.Exists(AppExe); } }

    /// <summary>Ask a running Phấn Trắng to close (it restores the mouse pointer itself); kill only if it hangs.</summary>
    static void StopRunningApp()
    {
        if (Process.GetProcessesByName("PhanTrang").Length == 0) return;
        try { using (var ev = EventWaitHandle.OpenExisting("PhanTrang-Quit")) ev.Set(); } catch { }
        for (int i = 0; i < 30 && Process.GetProcessesByName("PhanTrang").Length > 0; i++) Thread.Sleep(200);
        foreach (var p in Process.GetProcessesByName("PhanTrang"))
            try { p.Kill(); p.WaitForExit(3000); } catch { }
        SystemParametersInfo(0x57, 0, IntPtr.Zero, 0);   // never leave the pointer invisible
    }

    public static void Install(bool startMenu, bool desktop, bool autostart, Action<int> progress)
    {
        StopRunningApp();
        string dir = InstallDir;
        Directory.CreateDirectory(dir);
        string root = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("PhanTrang.payload.zip"))
        using (var zip = new ZipArchive(s, ZipArchiveMode.Read))
        {
            var entries = zip.Entries.Where(e => e.Name.Length > 0).ToList();
            for (int i = 0; i < entries.Count; i++)
            {
                // refuse entries that would land outside the install folder ("..\" in a name)
                string target = Path.GetFullPath(Path.Combine(dir, entries[i].FullName));
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Gói cài đặt bị hỏng.");
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                entries[i].ExtractToFile(target, true);
                if (progress != null) progress((i + 1) * 90 / entries.Count);
            }
        }
        // the uninstaller is this same program
        File.Copy(Assembly.GetExecutingAssembly().Location, Path.Combine(dir, "Uninstall.exe"), true);

        if (startMenu) Shortcut(StartMenuLink, AppExe, "Viết, vẽ, chỉ bài trên màn hình đang chiếu");
        if (desktop) Shortcut(DesktopLink, AppExe, "Viết, vẽ, chỉ bài trên màn hình đang chiếu");
        using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (autostart) k.SetValue(KeyName, "\"" + AppExe + "\"");
            else if (k.GetValue(KeyName) != null) k.SetValue(KeyName, "\"" + AppExe + "\"");   // keep the teacher's choice, point it at the new copy
        }
        using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            k.SetValue("DisplayName", AppName);
            k.SetValue("DisplayVersion", Version);
            k.SetValue("Publisher", "Viên Thế Hưng");
            k.SetValue("DisplayIcon", AppExe);
            k.SetValue("InstallLocation", dir);
            k.SetValue("UninstallString", "\"" + Path.Combine(dir, "Uninstall.exe") + "\" /uninstall");
            k.SetValue("QuietUninstallString", "\"" + Path.Combine(dir, "Uninstall.exe") + "\" /uninstall /S");
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            long kb = new DirectoryInfo(dir).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) / 1024;
            k.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue, kb), RegistryValueKind.DWord);
        }
        if (progress != null) progress(100);
    }

    static int Uninstall(bool silent)
    {
        if (!silent && MessageBox.Show("Gỡ Phấn Trắng khỏi máy này?\n\nBài giảng và cài đặt của thầy cô (trong thư mục AppData\\Roaming\\PhanTrang) được giữ lại.",
                "Gỡ Phấn Trắng", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return 1;
        StopRunningApp();
        foreach (var l in new[] { StartMenuLink, DesktopLink }) try { if (File.Exists(l)) File.Delete(l); } catch { }
        try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true)) if (k != null) k.DeleteValue(KeyName, false); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }
        string dir = InstallDir;
        // this program runs from the folder it removes: delete everything else now, the rest a moment after it exits
        foreach (var f in Directory.Exists(dir) ? Directory.GetFiles(dir, "*", SearchOption.AllDirectories) : new string[0])
            if (!f.EndsWith("Uninstall.exe", StringComparison.OrdinalIgnoreCase)) try { File.Delete(f); } catch { }
        try
        {
            Process.Start(new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"" + dir + "\"")
            { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
        }
        catch { }
        if (!silent) MessageBox.Show("Đã gỡ Phấn Trắng.", "Gỡ Phấn Trắng", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return 0;
    }

    public static void Launch() { try { Process.Start(new ProcessStartInfo(AppExe) { UseShellExecute = true, WorkingDirectory = InstallDir }); } catch { } }

    // ---- .lnk through the Windows shell (IShellLink) ----

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")] class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder f, int c, IntPtr fd, int fl);
        void GetIDList(out IntPtr p); void SetIDList(IntPtr p);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder n, int c);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string n);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder d, int c);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string d);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder a, int c);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string a);
        void GetHotkey(out short h); void SetHotkey(short h);
        void GetShowCmd(out int s); void SetShowCmd(int s);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder p, int c, out int i);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string p, int i);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string p, int r);
        void Resolve(IntPtr h, int f);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string f);
    }

    static void Shortcut(string lnk, string target, string description)
    {
        var link = (IShellLinkW)new ShellLink();
        link.SetPath(target);
        link.SetWorkingDirectory(Path.GetDirectoryName(target));
        link.SetDescription(description);
        link.SetIconLocation(target, 0);
        ((System.Runtime.InteropServices.ComTypes.IPersistFile)link).Save(lnk, true);
    }
}

/// <summary>One small window in the Phấn Trắng look: what will happen, three choices, one button.</summary>
class SetupForm : Form
{
    readonly CheckBox startMenu, desktop, autostart;
    readonly Button go;
    readonly ProgressBar bar;
    readonly Label status;
    bool done;
    static readonly Color Paper = Color.FromArgb(251, 248, 239), Green = Color.FromArgb(30, 74, 59), Board = Color.FromArgb(36, 83, 63),
        Frame = Color.FromArgb(110, 82, 52), Chalk = Color.FromArgb(244, 241, 230), Yellow = Color.FromArgb(242, 194, 48), Dim = Color.FromArgb(107, 100, 85);
    readonly float k;
    bool installing;
    int S(double v) { return (int)Math.Round(v * k); }

    /// <summary>The window cannot be closed while files are being copied: a half-done install would be left behind.</summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (installing && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; return; }
        base.OnFormClosing(e);
    }

    /// <summary>Runs 'a' on the window's thread, unless the window is already gone (Windows shutting down).</summary>
    void OnUi(Action a)
    {
        try { if (IsHandleCreated && !IsDisposed) BeginInvoke(a); }
        catch (InvalidOperationException) { }
    }

    public SetupForm()
    {
        k = DeviceDpi / 96f;
        Text = "Cài đặt Phấn Trắng";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(S(520), S(400));
        BackColor = Paper;
        Font = new Font("Segoe UI", 10.5f);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        bool upgrade = Setup.IsInstalled;
        status = new Label { AutoSize = false, Location = new Point(S(32), S(150)), Size = new Size(S(456), S(48)), ForeColor = Dim,
            Text = upgrade ? "Máy đã có Phấn Trắng. Bản " + Setup.Version + " sẽ được cài đè lên, bài giảng và cài đặt vẫn giữ nguyên." : "Cài cho tài khoản Windows này. Không cần quyền quản trị, không cần mạng." };
        startMenu = new CheckBox { Text = "Thêm vào menu Start", Checked = true, AutoSize = true, Location = new Point(S(32), S(204)) };
        desktop = new CheckBox { Text = "Tạo lối tắt trên màn hình nền", Checked = !upgrade, AutoSize = true, Location = new Point(S(32), S(234)) };
        autostart = new CheckBox { Text = "Mở cùng Windows (chờ sẵn ở khay hệ thống)", Checked = false, AutoSize = true, Location = new Point(S(32), S(264)) };
        bar = new ProgressBar { Location = new Point(S(32), S(304)), Size = new Size(S(456), S(8)), Visible = false };
        go = new Button { Text = upgrade ? "Cập nhật" : "Cài đặt", Size = new Size(S(150), S(42)), Location = new Point(S(338), S(334)), FlatStyle = FlatStyle.Flat, BackColor = Green, ForeColor = Chalk, Font = new Font("Segoe UI Semibold", 11f) };
        go.FlatAppearance.BorderSize = 0;
        var cancel = new Button { Text = "Thôi", Size = new Size(S(100), S(42)), Location = new Point(S(226), S(334)), FlatStyle = FlatStyle.Flat, BackColor = Color.White };
        cancel.FlatAppearance.BorderColor = Color.FromArgb(207, 197, 172);
        cancel.Click += (s, e) => Close();
        go.Click += (s, e) => Go(cancel);
        Controls.AddRange(new Control[] { status, startMenu, desktop, autostart, bar, go, cancel });
        AcceptButton = go;
        CancelButton = cancel;
    }

    void Go(Button cancel)
    {
        if (done) { Setup.Launch(); Close(); return; }
        go.Enabled = cancel.Enabled = startMenu.Enabled = desktop.Enabled = autostart.Enabled = false;
        bar.Visible = true;
        status.Text = "Đang cài…";
        installing = true;
        bool sm = startMenu.Checked, dt = desktop.Checked, au = autostart.Checked;
        var worker = new Thread(() =>
        {
            Exception err = null;
            try { Setup.Install(sm, dt, au, p => OnUi(() => bar.Value = Math.Min(100, p))); }
            catch (Exception e) { err = e; }
            OnUi(() =>
            {
                installing = false;
                if (err != null)
                {
                    status.Text = "Chưa cài được: " + (err is IOException ? "có tệp đang bị chương trình khác giữ. Thầy cô đóng Phấn Trắng rồi thử lại." : err.Message);
                    go.Enabled = cancel.Enabled = true;
                    return;
                }
                done = true;
                status.Text = "Xong! Phấn Trắng đã sẵn sàng. Lần sau mở từ menu Start" + (dt ? " hoặc biểu tượng trên màn hình nền." : ".");
                go.Text = "Mở Phấn Trắng";
                go.Enabled = true;
                cancel.Text = "Đóng"; cancel.Enabled = true;
            });
        });
        worker.IsBackground = true;
        worker.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var board = new Rectangle(0, 0, ClientSize.Width, S(128));
        using (var b = new SolidBrush(Board)) g.FillRectangle(b, board);
        using (var p = new Pen(Frame, S(8))) g.DrawRectangle(p, S(4), S(4), board.Width - S(8), board.Height - S(8));
        using (var f = new Font("Segoe UI Semibold", 22f)) TextRenderer.DrawText(g, "Phấn Trắng", f, new Point(S(30), S(26)), Chalk);
        using (var f = new Font("Segoe UI", 11f)) TextRenderer.DrawText(g, "Phiên bản " + Setup.Version + " · Phát triển bởi Viên Thế Hưng", f, new Point(S(33), S(78)), Yellow);
    }
}
