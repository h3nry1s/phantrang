using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PhanTrang
{
    /// <summary>The tray icon and its menu, the app's windows (settings, guide, welcome), quitting and the self-test switches.</summary>
    sealed partial class TeacherApp
    {
        readonly NotifyIcon tray = new NotifyIcon();
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        Font menuFont;
        SettingsWindow settingsWin;
        WelcomeWindow welcome;
        GuideWindow guide;

        void SetUpTray()
        {
            tray.Icon = TrayIcon(false);
            tray.Text = "Phấn Trắng – bảng xanh trên mọi màn hình";
            tray.Visible = true;
            tray.ContextMenuStrip = menu;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowTrayMenu(); };   // left or right: the same menu
            tray.BalloonTipClicked += (s, e) => ShowSettings();
            menu.Renderer = new PaperMenuRenderer();
            menu.Opening += (s, e) => { FillMenu(); MenuShown(true); };
            menu.Closed += (s, e) => MenuShown(false);
        }

        /// <summary>
        /// Opens the tray menu as a right click would. NotifyIcon only does that itself for the right button; its own
        /// (private) way also brings the menu to the front so that clicking elsewhere closes it, so it is used when
        /// present, with a plain Show at the pointer as the fallback.
        /// </summary>
        void ShowTrayMenu()
        {
            try
            {
                var show = typeof(NotifyIcon).GetMethod("ShowContextMenu", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (show != null) { show.Invoke(tray, null); return; }
            }
            catch (Exception e) { Log.Write("mở menu khay: " + e.Message); }
            menu.Show(Cursor.Position);
        }

        void MenuShown(bool open)
        {
            menuOpen = open;
            if (!on) return;
            UpdateInput();
            Frame();
        }

        /// <summary>Swaps the tray icon (a yellow dot while on) and frees the old one: Icon.FromHandle does not own its handle.</summary>
        void SetTrayIcon(bool active)
        {
            var old = tray.Icon;
            tray.Icon = TrayIcon(active);
            if (old == null) return;
            IntPtr handle = old.Handle;
            old.Dispose();
            Win32.DestroyIcon(handle);
        }

        static Icon TrayIcon(bool active)
        {
            using (var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    Draw.Logo(g, new RectangleF(0, 0, 32, 32));
                    if (active)
                        using (var dot = new SolidBrush(Theme.Yellow))
                        using (var ring = new Pen(Theme.Green, 1.5f))
                        {
                            g.FillEllipse(dot, 21, 21, 10, 10);
                            g.DrawEllipse(ring, 21, 21, 10, 10);
                        }
                }
                return Icon.FromHandle(bmp.GetHicon());   // GetHicon copies the pixels, so the bitmap can go
            }
        }

        // ---- the menu ----

        static string ShortcutText(KeyCombo k) { return k.IsNone ? null : k.Display.Replace(" + ", "+"); }

        float MenuScale { get { return menu.DeviceDpi / 96f; } }

        /// <summary>A line icon drawn at the menu's DPI.</summary>
        Image MenuIcon(string icon, Color c)
        {
            int size = (int)Math.Round(20 * MenuScale / 2) * 2;
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp)) ToolIcons.Draw(g, icon, new Rectangle(0, 0, size, size), c, false);
            return bmp;
        }

        ToolStripMenuItem Entry(string text, Action pick, string icon = null, string keys = null)
        {
            var item = new ToolStripMenuItem(text);
            if (icon != null) item.Image = MenuIcon(icon, Theme.Text);
            if (keys != null) item.ShortcutKeyDisplayString = keys;
            if (pick != null) item.Click += (s, e) => Post(pick);   // after the menu has closed
            int v = (int)(5 * MenuScale);
            item.Padding = new Padding(0, v, 0, v);
            return item;
        }

        ToolStripLabel Heading(string text, string font, float size, Color color, Padding pad)
        {
            return new ToolStripLabel(text) { Font = Fonts.Get(font, size * MenuScale), ForeColor = color, Padding = pad };
        }

        /// <summary>Menus rebuild on every opening: free the icons and fonts of the previous one.</summary>
        /// <remarks>
        /// The items are taken out of the menu first. Freeing them while still in it made the menu lay itself out again
        /// after every one, measuring text of items already disposed ("Parameter is not valid" in the log).
        /// </remarks>
        static void Release(ToolStripItemCollection items)
        {
            var list = items.Cast<ToolStripItem>().ToList();
            items.Clear();
            foreach (var item in list)
            {
                var sub = item as ToolStripMenuItem;
                if (sub != null && sub.HasDropDownItems) Release(sub.DropDownItems);
                var img = item.Image;
                item.Image = null;
                if (img != null) img.Dispose();
                item.Dispose();
            }
        }

        /// <summary>The tray menu, kept short: on/off, the board and lessons, then settings, the guide and quit.</summary>
        void FillMenu()
        {
            Release(menu.Items);
            float k = MenuScale;
            var previous = menuFont;
            menuFont = Fonts.Get("ui", 14 * k);
            menu.Font = menuFont;
            if (previous != null) previous.Dispose();   // only the font made here: the default one is shared by every menu
            menu.ShowImageMargin = true;
            menu.ImageScalingSize = new Size((int)(20 * k), (int)(20 * k));
            menu.MinimumSize = new Size((int)(270 * k), 0);

            menu.Items.Add(Heading("Phấn Trắng", "uiBold", 14.5f, Theme.Green, new Padding(0, (int)(6 * k), 0, 0)));
            menu.Items.Add(Heading(on ? "Đang bật trên màn hình" : "Đang tắt · chờ ở khay hệ thống", "ui", 12.5f, Theme.Dim, new Padding(0, 0, 0, (int)(6 * k))));
            menu.Items.Add(new ToolStripSeparator());

            var power = Entry(on ? "Tắt Phấn Trắng" : "Bắt đầu dạy", Toggle, on ? "power" : "mode:point", ShortcutText(Settings.ToggleKey));
            power.Font = Fonts.Get("uiSemi", 14 * k);
            menu.Items.Add(power);
            menu.Items.Add(Entry(on && Stage.Book.OnBoard ? "Đóng bảng" : "Mở bảng", ToggleBoard, "board", ShortcutText(Settings.BoardKey)));
            menu.Items.Add(LessonMenu());
            menu.Items.Add(ClassMenu());
            if (on && Stage.HasInk) menu.Items.Add(Entry("Xoá hết nét", ClearInk, "clear", ShortcutText(Settings.ClearKey)));

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Entry("Cài đặt…", ShowSettings, "gear"));
            menu.Items.Add(Entry("Hướng dẫn sử dụng", ShowGuide, "info"));
            menu.Items.Add(new ToolStripSeparator());
            var quit = Entry("Thoát Phấn Trắng", Quit);
            quit.ForeColor = Theme.Red;
            quit.Image = MenuIcon("exit", Theme.Red);
            menu.Items.Add(quit);
        }

        ToolStripMenuItem LessonMenu()
        {
            var lesson = Entry("Bài giảng", null, "newlesson");
            lesson.DropDown.Renderer = menu.Renderer;
            lesson.DropDown.Font = menu.Font;
            var items = lesson.DropDownItems;
            bool any = Stage.Book.HasLesson;
            items.Add(Entry("Soạn bài mới", NewLesson, "newlesson"));
            if (PowerPointOn) items.Add(Entry("Mở bài PowerPoint để viết lên…", OpenPptx, "open"));
            items.Add(Entry("Thêm ảnh vào trang…", AddImage, "addimage"));
            var pdf = Entry("Xuất thành PDF…", ExportPdf, "pdf"); pdf.Enabled = any; items.Add(pdf);
            if (PowerPointOn)
            {
                var pptx = Entry("Xuất thành PowerPoint…", () => ExportPptx(false), "pptx"); pptx.Enabled = any; items.Add(pptx);
                var addPptx = Entry("Thêm vào bài PowerPoint có sẵn…", () => ExportPptx(true), "addpptx"); addPptx.Enabled = any; items.Add(addPptx);
            }
            var export = Entry("Xuất các trang thành ảnh…", ExportImages, "image"); export.Enabled = any; items.Add(export);

            // spare copies Phấn Trắng saved by itself (when switched off, or before a new lesson)
            bool spare = File.Exists(Lesson.AutoSavePath);
            var earlier = Lesson.OlderAutoSaves().Take(3).ToList();
            if (spare || earlier.Count > 0)
            {
                items.Add(new ToolStripSeparator());
                items.Add(Heading("Bản tự lưu", "uiBold", 12, Theme.Green, Padding.Empty));
            }
            if (spare) items.Add(Entry("Bài đang soạn dở", () => OpenSpare(Lesson.AutoSavePath)));
            foreach (var e in earlier) { string p = e.Key; items.Add(Entry("Bản tự lưu " + e.Value.ToString("dd/MM HH:mm"), () => OpenSpare(p))); }
            return lesson;
        }

        // ---- windows ----

        public void ShowSettingsLater() { Post(ShowSettings); }
        public void QuitLater() { Post(Quit); }

        public void ShowSettings()
        {
            if (settingsWin == null || settingsWin.IsDisposed)
            {
                settingsWin = new SettingsWindow(this);
                settingsWin.FormClosed += (s, e) => settingsWin = null;
                settingsWin.Show();
            }
            if (settingsWin.WindowState == FormWindowState.Minimized) settingsWin.WindowState = FormWindowState.Normal;
            settingsWin.Activate();
        }

        void ShowWelcome()
        {
            Settings.WelcomeShown = true;
            if (welcome != null && !welcome.IsDisposed) { welcome.Activate(); return; }
            welcome = new WelcomeWindow();
            welcome.StartTeaching += () => After(150, TurnOn);
            welcome.OpenGuide += ShowGuide;
            welcome.FormClosed += (s, e) => welcome = null;
            welcome.Show();
            welcome.Activate();
        }

        public void ShowGuide() { ShowGuide(null); }

        public void ShowGuide(string topic)
        {
            if (guide == null || guide.IsDisposed)
            {
                guide = new GuideWindow();
                guide.OpenSettings += ShowSettings;
                guide.FormClosed += (s, e) => guide = null;
                guide.Show();
            }
            if (topic != null) guide.ShowTopic(topic);
            if (guide.WindowState == FormWindowState.Minimized) guide.WindowState = FormWindowState.Normal;
            guide.Activate();
        }

        void Quit()
        {
            AutoSave();
            TurnOffSafely();
            Usage.Flush();
            gate.Stop();
            if (settingsWin != null && !settingsWin.IsDisposed) settingsWin.Close();
            tray.Visible = false;
            tray.Dispose();
            BlankCursor.Reset();
            ExitThread();
        }

        // ---- start-up switches (also used by the self-tests to photograph each state) ----

        static string Value(string[] args, string name)
        {
            var a = args.FirstOrDefault(x => x.StartsWith(name + "="));
            return a == null ? null : a.Substring(name.Length + 1);
        }

        void Startup(string[] args)
        {
            bool demo = args.Contains("--demo") || args.Contains("--demo-type");
            if (!args.Contains("--on") && !demo) After(250, UpdateQuickButton);
            if (!Settings.WelcomeShown || args.Contains("--welcome")) After(300, ShowWelcome);
            string page = Value(args, "--page");
            if (args.Contains("--settings") || page != null) After(300, () => { ShowSettings(); if (page != null) settingsWin.ShowPage(page); });
            if (args.Contains("--on")) After(400, TurnOn);
            if (args.Contains("--demo-image")) After(700, DemoPicture);
            string shot = Value(args, "--menu-shot");
            if (shot != null) After(500, () => MenuPicture(shot));
            var guideArg = args.FirstOrDefault(a => a.StartsWith("--guide"));
            if (guideArg != null) After(300, () => ShowGuide(guideArg.Contains("=") ? guideArg.Substring(8) : null));
            if (args.Contains("--demo")) After(500, () => Demo(args.Contains("--demo-board")));
            if (args.Contains("--demo-type")) After(500, () => { Demo(false); toolbar.CloseChoices(); SetTool("type"); Refresh(); });
            string tool = Value(args, "--tool"), choices = Value(args, "--popup");
            if (tool != null || choices != null)
                After(900, () => { if (tool != null) SetTool(tool); if (choices != null) toolbar.ToggleChoices(choices); Refresh(); });
        }

        /// <summary>Self-test: the tray menu drawn into a PNG (no screen capture involved).</summary>
        void MenuPicture(string path)
        {
            menu.Show(new Point(-4000, -4000));
            After(300, () =>
            {
                using (var bmp = new Bitmap(menu.Width, menu.Height))
                {
                    menu.DrawToBitmap(bmp, new Rectangle(0, 0, menu.Width, menu.Height));
                    bmp.Save(path, ImageFormat.Png);
                }
                menu.Close();
            });
        }

        /// <summary>Self-test: a picture placed on a white board, chosen and turned a little.</summary>
        void DemoPicture()
        {
            TurnOn();
            SetBoard("white");
            using (var bmp = new Bitmap(600, 400))
            using (var png = new MemoryStream())
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.FromArgb(234, 242, 252));
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var pen = new Pen(Color.FromArgb(30, 90, 168), 6)) g.DrawPolygon(pen, new[] { new Point(80, 340), new Point(520, 340), new Point(80, 60) });
                    using (var f = Fonts.Get("uiBold", 40))
                    {
                        g.DrawString("A", f, Brushes.Black, 40, 20);
                        g.DrawString("B", f, Brushes.Black, 30, 330);
                        g.DrawString("C", f, Brushes.Black, 530, 320);
                    }
                }
                bmp.Save(png, ImageFormat.Png);
                var picture = PictureMark.FromBytes(png.ToArray());
                var m = layer.Monitor;
                Stage.Pictures.Begin(picture, new PointF(m.Width * 0.5f, m.Height * 0.45f));
                Stage.Pictures.Drop(new PointF(m.Width * 0.5f, m.Height * 0.45f));
                SetTool("image");
                picture.Angle = 12;
                Stage.Invalidate();
            }
            Refresh();
        }

        /// <summary>Self-test: the green board with a sample of every tool, to check how everything looks.</summary>
        void Demo(bool openBoardChoices)
        {
            TurnOn();
            var m = layer.Monitor;
            float W = m.Width, H = m.Height;
            SetBoard("green");
            Settings.InkChoice = "white";
            Stage.InkColor = Catalog.InkColor;
            Stage.Caret = new PointF(W * 0.06f, H * 0.06f);
            var lines = new[]
            {
                new[] { "chalk", "Thứ Năm, ngày 7 tháng 11" }, new[] { "mali", "Bài 3: Cộng hai phân số cùng mẫu" },
                new[] { "print", "1/4 + 2/4 = 3/4  ·  x² ≥ 0  ·  √16 = 4" }, new[] { "mali", "∠ABC = 90°,  AB ⊥ BC,  π ≈ 3,14" },
                new[] { "slant", "Giữ nguyên mẫu số, cộng các tử số." }, new[] { "brush", "Chúc mừng cả lớp!" },
            };
            foreach (var line in lines) { Stage.ShowTyping(line[1], line[0], H * 0.052f); Stage.CommitTyping(); }
            Stage.ShowTyping("Bạn Lan lên bảng", "mali", H * 0.052f);
            Settings.InkChoice = "yellow";
            Stage.InkColor = Catalog.InkColor;
            Stage.BeginShape("arrow", new PointF(W * 0.55f, H * 0.33f), new PointF(W * 0.66f, H * 0.24f));
            Stage.EndAll();
            Stage.BeginShape("ellipse", new PointF(W * 0.045f, H * 0.255f), new PointF(W * 0.38f, H * 0.37f));
            Stage.EndAll();
            float x = W * 0.6f;
            foreach (var s in new[] { "diem10", "hoa", "dung", "sai", "sao" }) { Stage.AddStamp(s, new PointF(x, H * 0.5f)); x += W * 0.075f; }
            Stage.AddStamp("totlam", new PointF(W * 0.72f, H * 0.66f));
            Settings.InkChoice = "red";
            Settings.Tool = "stamp";
            if (openBoardChoices) toolbar.ToggleChoices("board");
            Refresh();
        }
    }

    /// <summary>Windows shell surfaces where the chalk steps aside and clicks go through: taskbar, menus, flyouts — and our own forms.</summary>
    static class ShellZones
    {
        static readonly HashSet<string> ShellClasses = new HashSet<string>
        {
            "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "#32768", "NotifyIconOverflowWindow",
            "TopLevelWindowForOverflowXamlIsland", "Xaml_WindowedPopupClass", "Microsoft.UI.Content.PopupWindowSiteBridge",
            "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow",
        };
        const int DWMWA_CLOAKED = 14;
        static readonly StringBuilder className = new StringBuilder(256);
        static readonly List<RECT> found = new List<RECT>();
        static readonly WindowVisitor visitor = Visit;   // kept alive while Windows calls it
        static readonly int ownProcess = Process.GetCurrentProcess().Id;

        public static RECT[] Scan()
        {
            found.Clear();
            Win32.EnumWindows(visitor, IntPtr.Zero);
            return found.ToArray();
        }

        static bool IsOurForm(IntPtr hwnd, string cls)
        {
            if (!cls.StartsWith("WindowsForms10.") || !cls.Contains("Window.808")) return false;
            int pid;
            Win32.GetWindowThreadProcessId(hwnd, out pid);
            return pid == ownProcess;
        }

        static bool Visit(IntPtr hwnd, IntPtr data)
        {
            if (!Win32.IsWindowVisible(hwnd)) return true;
            className.Length = 0;
            Win32.GetClassName(hwnd, className, className.Capacity);
            string cls = className.ToString();
            if (!ShellClasses.Contains(cls) && !IsOurForm(hwnd, cls)) return true;
            int cloaked;
            if (Win32.DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0) return true;   // on another virtual desktop
            RECT r;
            if (Win32.GetWindowRect(hwnd, out r) && r.Width > 0 && r.Height > 0) found.Add(r);
            return true;
        }
    }
}
