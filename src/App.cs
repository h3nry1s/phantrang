using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PhanTrang
{
    /// <summary>
    /// The running app: it waits in the tray, and while switched on it covers the monitor under the mouse with the
    /// see-through layer, shows the toolbar, hides the Windows pointer behind its own, and turns clicks into chalk,
    /// ink, stamps and text. This part holds the on/off switch, the frame loop and the mouse handling; the toolbar,
    /// lessons and tray live in the other TeacherApp files.
    /// </summary>
    sealed partial class TeacherApp : ApplicationContext
    {
        enum Shortcut { Toggle = 1, Clear = 2, Escape = 3, Undo = 4, Redo = 5, Board = 6 }

        readonly Control ui = new Control();          // a handle on the UI thread to post work to
        readonly ScreenLayer layer = new ScreenLayer();
        readonly Toolbar toolbar = new Toolbar();
        readonly ClickGate gate = new ClickGate();
        readonly HotKeyWindow hotKeys = new HotKeyWindow();
        readonly QuickButton quick = new QuickButton();
        readonly HoldGuard holdGuard = new HoldGuard();
        readonly RawMouse rawMouse = new RawMouse();
        readonly GeoTools geo = new GeoTools();
        readonly List<ClassCard> cards = new List<ClassCard>();
        readonly System.Windows.Forms.Timer frames = new System.Windows.Forms.Timer();
        public readonly Sounds Sounds = new Sounds();

        bool on;                                       // switched on
        bool shortcutsPaused, escBound, editBound;
        bool menuOpen, dialogOpen, collapsed, windowsPointer;
        bool leftHeld, rightHeld;
        PointF pressedAt;
        double lastFrame, lastMoved, showBarUntil, lastZoneScan = -1, lastRaise, quietUntil, lastGateCheck;
        int lastX = int.MinValue, lastY;
        int movePosted, gateRestarts;
        double lastGateLog = -1000;
        RECT[] shellZones = new RECT[0];
        RECT work;
        double screenScale = 1;

        public event Action ActiveChanged;
        public bool IsActive { get { return on; } }
        Stage Stage { get { return layer.Stage; } }
        static double Now { get { return Clock.Now; } }
        bool MouseMode { get { return Settings.Tool == "mouse"; } }
        float TypingPx { get { return (float)(Catalog.TextSizeFraction * (layer.Monitor.Height > 0 ? layer.Monitor.Height : 1080)); } }

        public TeacherApp(string[] args)
        {
            ui.CreateControl();
            gate.Report = OnGate;
            rawMouse.Button += (b, down) => { if (down) holdGuard.RawDown(b, Now); else holdGuard.RawUp(b, Now); };
            gate.HoleTest = geo.Hit;
            geo.ShiftHeld = () => ShiftDown;
            geo.StrokeBegin = p => { if (!on) return; var at = new PointF(p.X - layer.Monitor.Left, p.Y - layer.Monitor.Top); Stage.EndAll(); Stage.BeginStroke(at, false); };
            geo.StrokeTo = p => { if (on && Stage.IsDrawing) Stage.ExtendStroke(new PointF(p.X - layer.Monitor.Left, p.Y - layer.Monitor.Top)); };
            geo.StrokeEnd = () => { if (!on) return; Stage.EndAll(); Refresh(); };
            geo.Changed += () => { toolbar.Rebuild(); if (on) Frame(); };

            toolbar.MainButtons = MainButtons;
            toolbar.ChoiceButtons = ChoiceButtons;
            toolbar.Pressed = OnToolbar;

            hotKeys.Fired += id => OnShortcut((Shortcut)id);
            ApplyShortcuts();

            frames.Interval = 10;
            frames.Tick += (s, e) => Frame();

            SetUpTray();
            SystemEvents.SessionEnding += (s, e) => BlankCursor.Reset();
            Settings.Changed += ApplySettings;
            ApplySettings();
            quick.Clicked += () => Post(TurnOn);
            Startup(args);
            Usage.Start();
        }

        /// <summary>Runs 'a' on the UI thread after 'ms' milliseconds.</summary>
        static void After(int ms, Action a)
        {
            var t = new System.Windows.Forms.Timer { Interval = Math.Max(1, ms) };
            t.Tick += (s, e) => { t.Dispose(); a(); };
            t.Start();
        }

        /// <summary>Queues 'a' for the UI thread (safe from any thread, and harmless while shutting down).</summary>
        void Post(Action a)
        {
            try { if (ui.IsHandleCreated && !ui.IsDisposed) ui.BeginInvoke(a); }
            catch (InvalidOperationException) { }
        }

        // ---- settings & shortcuts ----

        void ApplySettings()
        {
            var look = Stage.Look;
            look.Kind = Settings.Pointer;
            look.Size = Settings.PointerSize;
            look.Shadow = Settings.Shadow;
            look.EraserRadius = Settings.Tool == "eraser" ? Stage.InkWidth * 3.5f : 0;
            Stage.InkColor = Catalog.InkColor;
            Stage.WidthFactor = Catalog.WidthFactor;
            Stage.FadeAfter = Settings.FadeSeconds;
            Stage.VanishAfter = Settings.VanishSeconds;
            Stage.TypeLines = Settings.TypeLines;
            Stage.Invalidate();
            if (Stage.Typing && typePanel != null) Stage.ShowTyping(typePanel.Typed, Settings.TextFont, TypingPx);
            if (typePanel != null) typePanel.Refresh2();
            if (typePanel != null && !typePanel.IsDisposed && Math.Abs(typePanel.SizeSetting - Settings.TypePanelSize) > 0.001) Post(RebuildTypePanel);
            toolbar.Rebuild();
            toolbar.ReloadPlace();
            gate.BypassKey = Settings.InteractVk;
            UpdateQuickButton();
            if (on) { UpdateInput(); Frame(); }
        }

        void UpdateQuickButton() { if (!on && Settings.QuickButton) quick.Show(); else quick.Hide(); }

        /// <summary>(Re)binds the user's three global shortcuts, unless a shortcut box in Settings is listening.</summary>
        public void ApplyShortcuts()
        {
            foreach (var s in new[] { Shortcut.Toggle, Shortcut.Clear, Shortcut.Board }) hotKeys.Unbind((int)s);
            if (shortcutsPaused) return;
            Bind(Shortcut.Toggle, Settings.ToggleKey);
            Bind(Shortcut.Clear, Settings.ClearKey);
            Bind(Shortcut.Board, Settings.BoardKey);
        }

        void Bind(Shortcut id, KeyCombo combo)
        {
            if (!combo.IsNone && !hotKeys.Bind((int)id, combo.Mods, combo.Key)) Log.Write("phím tắt " + combo.Display + " đã bị ứng dụng khác dùng");
        }

        /// <summary>While a shortcut box in Settings has focus, pressing the current shortcut must reach it.</summary>
        public void PauseShortcuts(bool pause) { shortcutsPaused = pause; ApplyShortcuts(); }

        void OnShortcut(Shortcut s)
        {
            switch (s)
            {
                case Shortcut.Toggle: Toggle(); break;
                case Shortcut.Clear: ClearInk(); break;
                case Shortcut.Board: ToggleBoard(); break;
                case Shortcut.Undo: Stage.Undo(); Refresh(); break;
                case Shortcut.Redo: Stage.Redo(); Refresh(); break;
                case Shortcut.Escape:
                    // Esc steps back one level: placing a picture, a chosen picture, open choices, then off
                    if (Stage.Pictures.Placing) { Stage.Pictures.Cancel(); UpdateInput(); Frame(); }
                    else if (Stage.Pictures.Chosen != null) { Stage.Pictures.Choose(null); Refresh(); }
                    else if (toolbar.ChoicesOpen) toolbar.CloseChoices();
                    else TurnOff();
                    break;
            }
        }

        /// <summary>
        /// Who gets the mouse and keys right now. Clicks are captured while on, unless in Chuột mode (except to put
        /// down a picture) or while a menu or dialog is open. Esc and Ctrl+Z/Y are ours only while clicks are.
        /// </summary>
        void UpdateInput()
        {
            bool capture = on && !menuOpen && !dialogOpen && (!MouseMode || Stage.Pictures.Placing);
            gate.Capturing = capture;
            bool typingFocused = typePanel != null && typePanel.Visible;
            BindEscape(capture && !typingFocused);
            BindEdit(capture && !MouseMode && !(typingFocused && typePanel.ContainsFocus));
        }

        void BindEscape(bool want)
        {
            if (want == escBound) return;
            escBound = want;
            if (want) hotKeys.Bind((int)Shortcut.Escape, 0, Keys.Escape); else hotKeys.Unbind((int)Shortcut.Escape);
        }

        void BindEdit(bool want)
        {
            if (want == editBound) return;
            editBound = want;
            if (want)
            {
                hotKeys.Bind((int)Shortcut.Undo, Win32.Mod.Ctrl, Keys.Z);
                hotKeys.Bind((int)Shortcut.Redo, Win32.Mod.Ctrl, Keys.Y);
            }
            else
            {
                hotKeys.Unbind((int)Shortcut.Undo);
                hotKeys.Unbind((int)Shortcut.Redo);
            }
        }

        public void ToggleFromSettings() { Toggle(); }

        public void ClearInk()
        {
            Stage.ClearInk();
            if (typePanel != null && typePanel.Visible) Stage.ShowTyping(typePanel.Typed, Settings.TextFont, TypingPx);
            Refresh();
        }

        void Refresh()
        {
            toolbar.Rebuild();
            if (on) Frame();
        }

        // ---- on / off ----

        void Toggle() { if (on) TurnOff(); else TurnOn(); }

        void TurnOn()
        {
            if (on) return;
            if (!gate.Start())
            {
                // without the mouse gate every click would go to the slide underneath while we draw: stay off
                Log.Write("không cài được bộ bắt chuột (WH_MOUSE_LL)");
                gate.Stop();
                MessageBox.Show("Phấn Trắng chưa bật được vì Windows không cho theo dõi chuột lúc này.\n\nThầy cô thử lại sau ít giây, hoặc khởi động lại máy.",
                    "Phấn Trắng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Usage.Count("on");
            on = true;
            if (MouseMode) Settings.Tool = "pointer";   // switching on means: show the chalk
            windowsPointer = false;
            Stage.Look.Hidden = false;
            var screen = Win32.ScreenAtCursor();
            work = screen.Work; screenScale = screen.Scale;
            layer.Cover(screen.Bounds);
            ApplySettings();
            toolbar.Place(work, screenScale);
            layer.Show();
            toolbar.Show();
            showBarUntil = Now + 3;
            BlankCursor.Apply();
            UpdateInput();
            Win32.timeBeginPeriod(1);
            rawMouse.Start();
            lastFrame = lastMoved = lastGateCheck = Now;
            frames.Start();
            SetTrayIcon(true);
            quick.Hide();
            if (Settings.Tool == "type") OpenTyping();
            Frame();
            After(60, () => { if (on) Restack(); });
            if (ActiveChanged != null) ActiveChanged();
        }

        void TurnOff()
        {
            if (!on) return;
            CloseTyping();
            AutoSave();
            on = false;
            frames.Stop();
            Win32.timeEndPeriod(1);
            gate.Capturing = false;
            gate.Stop();
            BindEscape(false);
            rawMouse.Stop();
            BindEdit(false);
            leftHeld = rightHeld = false;
            Stage.EndAll();
            Stage.ClearScreen();
            Stage.SetBoard("none");
            layer.Hide();
            toolbar.Hide();
            geo.HideAll();
            BlankCursor.Lift();
            SetTrayIcon(false);
            UpdateQuickButton();
            var sw = settingsWin;
            if (sw != null && !sw.IsDisposed) Win32.SetWindowPos(sw.Handle, Win32.NotTopmost, 0, 0, 0, 0, Win32.Swp.KeepPlace);
            if (ActiveChanged != null) ActiveChanged();
        }

        /// <summary>Switch off after an unexpected error; whatever happens, the Windows pointer comes back.</summary>
        public void TurnOffSafely()
        {
            try { TurnOff(); }
            catch (Exception e) { Log.Write("tắt sau lỗi: " + e.Message); BlankCursor.Reset(); }
        }

        // ---- mouse ----

        /// <summary>Called on the gate's thread: only hands the event to the UI thread.</summary>
        void OnGate(GateEvent e)
        {
            switch (e.Kind)
            {
                case GateKind.Move:
                    // many moves arrive between frames; keep at most one waiting
                    if (Interlocked.Exchange(ref movePosted, 1) == 0)
                        Post(() => { Interlocked.Exchange(ref movePosted, 0); if (on && Now - lastFrame >= 1 / 144.0) Frame(); });
                    break;
                case GateKind.Down: Post(() => Pressed(e.Button, e.At)); break;
                case GateKind.Up: Post(() => Released(e.Button)); break;
                case GateKind.BypassDown: Post(BypassPressed); break;
                case GateKind.BypassUp: Post(() => { Stage.Look.Held = false; quietUntil = Now + 1; }); break;
            }
        }

        PointF ToLayer(POINT p) { var m = layer.Monitor; return new PointF(p.X - m.Left, p.Y - m.Top); }
        static bool ShiftDown { get { return Win32.KeyDown(Win32.Vk.Shift); } }

        void Pressed(int button, POINT p)
        {
            if (!on) return;
            toolbar.CloseChoices();
            Stage.Look.Press();
            var at = ToLayer(p);
            lastMoved = Now;

            if (Stage.Pictures.Placing)
            {
                // a left click puts the picture down; a right click changes the teacher's mind
                if (button == 0)
                {
                    Stage.Pictures.Drop(at);
                    Sounds.StampSound();
                    if (Settings.Tool != "image") SetTool("image");   // the new picture is chosen: arrange it straight away
                }
                else Stage.Pictures.Cancel();
                UpdateInput();
                Refresh();
                return;
            }

            string tool = Settings.Tool;
            if (button == 1 || tool == "pointer") { Stage.Look.Tap(at); Sounds.Tap(); }
            if (button == 1) { rightHeld = true; holdGuard.Pressed(1, Now); Frame(); return; }

            leftHeld = true;
            holdGuard.Pressed(0, Now);
            pressedAt = at;
            switch (tool)
            {
                case "stamp": Stage.AddStamp(Settings.StampKind, at); Sounds.StampSound(); toolbar.Rebuild(); break;
                case "eraser": Stage.EraseAt(at); break;
                case "image": Stage.Pictures.Grab(at); toolbar.Rebuild(); break;
                case "type":
                    // the click chooses where the next line goes; a half-typed line is kept where it was
                    if (typePanel != null && typePanel.Typed.Trim().Length > 0) { Stage.CommitTyping(); typePanel.ClearText(); }
                    Stage.Caret = new PointF(at.X, Settings.TypeLines ? Stage.SnapToLine(at.Y, TypingPx) : at.Y - TypingPx * 0.6f);
                    OpenTyping();
                    break;
            }
            Frame();
        }

        void Released(int button)
        {
            holdGuard.Released(button);
            if (button == 0) toolbar.EndDrag();
            if (button == 0 && leftHeld) { leftHeld = false; Stage.EndAll(); toolbar.Rebuild(); }
            else if (button == 1) rightHeld = false;
            if (!leftHeld && !rightHeld) Stage.Look.Held = false;
            if (on) Frame();
        }

        void BypassPressed()
        {
            if (!on) return;
            Stage.Look.Press();
            if (Settings.InteractKey == "alt")
            {
                // a lone Alt press and release would open the slide program's menu bar: send an unused key in between
                Win32.keybd_event(0xE8, 0, 0, UIntPtr.Zero);
                Win32.keybd_event(0xE8, 0, 2 /* KEYUP */, UIntPtr.Zero);
            }
            lastMoved = Now;
        }

        /// <summary>The left button is held and the mouse moved to 'at': continue whatever the tool does.</summary>
        void Dragged(PointF at)
        {
            float dx = at.X - pressedAt.X, dy = at.Y - pressedAt.Y;
            bool started = dx * dx + dy * dy >= 16;   // a few pixels, so a plain click draws nothing
            string tool = Settings.Tool;
            switch (tool)
            {
                case "draw":
                case "hl":
                case "vanish":
                    if (geo.Any)
                    {
                        // a line started beside a ruler or a set square runs along its edge
                        var m = layer.Monitor;
                        var q = new PointF(at.X + m.Left, at.Y + m.Top);
                        if (geo.Snap(new PointF(pressedAt.X + m.Left, pressedAt.Y + m.Top), ref q)) at = new PointF(q.X - m.Left, q.Y - m.Top);
                    }
                    if (!Stage.IsDrawing && started) Stage.BeginStroke(pressedAt, tool == "hl", tool == "vanish");
                    if (Stage.IsDrawing) Stage.ExtendStroke(at);
                    break;
                case "shape":
                    string kind = Settings.ShapeKind;
                    var end = ShiftDown ? ShapeMark.Constrain(kind, pressedAt, at) : at;
                    if (Stage.CurrentShape != null) Stage.UpdateShape(end);
                    else if (started) Stage.BeginShape(kind, pressedAt, end);
                    break;
                case "eraser": Stage.EraseAt(at); break;
                case "image": if (Stage.Pictures.Dragging) Stage.Pictures.Drag(at, ShiftDown); break;
            }
        }

        // ---- frames ----

        /// <summary>Shell surfaces (taskbar, menus, flyouts) and our own windows: there the Windows pointer comes back and clicks go through.</summary>
        RECT[] Zones(double now)
        {
            if (now - lastZoneScan > 0.1)
            {
                lastZoneScan = now;
                var zones = new List<RECT>(ShellZones.Scan());
                foreach (var f in Floating)
                {
                    RECT r;
                    if (f != null && !f.IsDisposed && f.Visible && f.WindowState != FormWindowState.Minimized && Win32.GetWindowRect(f.Handle, out r)) zones.Add(r);
                }
                shellZones = zones.ToArray();
            }
            return shellZones;
        }

        /// <summary>
        /// Windows quietly removes a low-level mouse hook that once answered too slowly. If the mouse keeps moving
        /// but the gate hears nothing, it is put back, so clicks never start going to the slide while drawing.
        /// </summary>
        void WatchGate(double now)
        {
            if (now - lastGateCheck < 1) return;
            lastGateCheck = now;
            if (gate.SilentFor < 0.5) { gateRestarts = 0; return; }   // it hears the mouse: all well
            // Some devices move the pointer without passing through low-level hooks; after a few tries that
            // changed nothing, stop retrying rather than restarting the hook every second.
            if (now - lastMoved >= 0.5 || gate.SilentFor <= 1.5 || gateRestarts >= 3) return;
            gateRestarts++;
            if (now - lastGateLog > 600) { lastGateLog = now; Log.Write("bộ bắt chuột không nhận được chuột, cài lại (lần " + gateRestarts + ")"); }
            if (!gate.Restart()) { Log.Write("cài lại bộ bắt chuột không được"); TurnOff(); }
            else UpdateInput();
        }

        /// <summary>
        /// Ends a press whose release Windows never passed to the gate (see <see cref="HoldGuard"/>), so a line
        /// never keeps following the mouse after the button is up.
        /// </summary>
        void CheckStuckButtons(double now)
        {
            for (int b = 0; b < 2; b++)
            {
                bool held = b == 0 ? leftHeld : rightHeld;
                if (!held || !holdGuard.Stuck(b, now)) continue;
                holdGuard.Released(b);
                Log.Write("nút chuột đã nhả nhưng không được báo, tự kết thúc (nút " + b + ")");
                int button = b;
                Post(() => Released(button));
            }
        }

        void Frame()
        {
            if (!on) return;
            double now = Now, dt = now - lastFrame;
            lastFrame = now;
            var p = Win32.CursorPos();
            if (p.X != lastX || p.Y != lastY) { lastX = p.X; lastY = p.Y; lastMoved = now; }
            WatchGate(now);
            CheckStuckButtons(now);
            if (!on) return;

            var screen = Win32.ScreenAt(p.X, p.Y);
            work = screen.Work; screenScale = screen.Scale;
            if (!screen.Bounds.SameAs(layer.Monitor))
            {
                // the mouse went to another monitor: the layer follows it
                if (leftHeld) { leftHeld = false; Stage.EndAll(); Stage.Look.Held = false; }
                layer.Cover(screen.Bounds);
                Stage.Look.EraserRadius = Settings.Tool == "eraser" ? Stage.InkWidth * 3.5f : 0;
                RefreshTyping();
                showBarUntil = Math.Max(showBarUntil, now + 1.5);
            }
            toolbar.Place(work, screenScale);

            var zones = Zones(now);
            bool overZone = zones.Any(z => z.Contains(p.X, p.Y));
            if (!overZone && geo.Hit(p.X, p.Y)) overZone = true;   // on a ruler or a protractor: the Windows pointer, to grab it
            // over the toolbar the Windows pointer comes back, so the chalk never hides the buttons
            if (toolbar.Revealed && toolbar.ScreenRects.Any(r => r.Contains(p.X, p.Y))) overZone = true;

            bool drawing = Stage.IsDrawing;
            bool bypass = gate.BypassHeld;
            bool wantWindowsPointer = !drawing && ((bypass && !Settings.InteractKeepsPointer) || overZone || menuOpen || dialogOpen || MouseMode || Settings.Tool == "image");
            if (wantWindowsPointer != windowsPointer)
            {
                windowsPointer = wantWindowsPointer;
                Stage.Look.Hidden = wantWindowsPointer;
                if (wantWindowsPointer) BlankCursor.Lift(); else BlankCursor.Apply();
            }

            // while a line is being drawn the bar steps back: faint (or gone, if the teacher chose so) and not in the way of the chalk
            toolbar.Drag();
            bool barWanted = Settings.ToolbarAlways || toolbar.Dragging || toolbar.Near(p.X, p.Y) || now < showBarUntil;
            toolbar.Reveal(barWanted && !drawing, barWanted ? Settings.ToolbarDim : 0);
            bool barMoving = toolbar.Animate(dt);
            toolbar.Hover(p.X, p.Y);

            var holes = new List<RECT>(zones);
            if (toolbar.Revealed) holes.AddRange(toolbar.ScreenRects);
            gate.Holes = holes.ToArray();
            UpdateInput();

            var at = ToLayer(p);
            if (leftHeld) Dragged(at);
            layer.Frame(at.X, at.Y, dt, now);

            if (now - lastRaise > 1) { lastRaise = now; Restack(); }

            // fast frames while something moves, slow ones while idle
            bool busy = now - lastMoved < 0.5 || Stage.IsSettling || Stage.Look.Held || leftHeld || barMoving || now < quietUntil;
            int interval = busy ? 10 : 50;
            if (frames.Interval != interval) frames.Interval = interval;
        }

        /// <summary>Keeps the layering right: the ink layer, then our windows above it, then the toolbar on top.</summary>
        void Restack()
        {
            if (dialogOpen) return;   // the open/save dialog stays above the board
            layer.Raise();
            foreach (var f in Floating)
                if (f != null && !f.IsDisposed && f.Visible && f.WindowState != FormWindowState.Minimized)
                    Win32.SetWindowPos(f.Handle, Win32.Topmost, 0, 0, 0, 0, Win32.Swp.KeepPlace);
            geo.Raise();
            toolbar.Raise();
        }
    }
}
