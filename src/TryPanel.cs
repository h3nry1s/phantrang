using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace PhanTrang
{
    /// <summary>
    /// "Thử ngay": a page of an exercise book inside the Settings window where the pointer can be tried, life-size,
    /// without switching Phấn Trắng on for the whole screen. Move to see it, click to tap, drag to draw.
    /// </summary>
    class TryPanel : Control
    {
        readonly TeacherApp app;
        readonly Stage stage = new Stage();
        readonly Timer ticker = new Timer { Interval = 10 };
        PixelBuffer pixels;
        Bitmap page;
        double last;
        PointF mouse, pressedAt;
        bool over, drawing, preciseTimer;
        static Cursor invisible;

        public TryPanel(TeacherApp app)
        {
            this.app = app;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
            Cursor = Invisible();
            ticker.Tick += (s, e) => Step();
            Settings.Changed += Restyle;
            Restyle();
            ticker.Start();
        }

        static Cursor Invisible()
        {
            if (invisible == null)
            {
                var andMask = new byte[128];
                for (int i = 0; i < andMask.Length; i++) andMask[i] = 0xFF;
                invisible = new Cursor(Win32.CreateCursor(IntPtr.Zero, 0, 0, 32, 32, andMask, new byte[128]));
            }
            return invisible;
        }

        /// <summary>Stops the timer and lets go of the settings; called when the window closes.</summary>
        public void Shutdown()
        {
            ticker.Stop();
            PreciseTimer(false);
            Settings.Changed -= Restyle;
        }

        void Restyle()
        {
            if (IsDisposed) return;
            var look = stage.Look;
            look.Kind = Settings.Pointer;
            look.Size = Settings.PointerSize;
            look.Shadow = Settings.Shadow;
            stage.InkColor = Catalog.InkColor;
            stage.WidthFactor = Catalog.WidthFactor;
            if (Width > 2 && Height > 2) { PaintPage(); stage.Invalidate(); }
            if (!ticker.Enabled) ticker.Start();
        }

        /// <summary>1 ms timer resolution only while the mouse is over the panel (it costs battery).</summary>
        void PreciseTimer(bool on)
        {
            if (on == preciseTimer) return;
            preciseTimer = on;
            if (on) Win32.timeBeginPeriod(1); else Win32.timeEndPeriod(1);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width < 2 || Height < 2) return;
            stage.RefH = Screen.FromControl(this).Bounds.Height;   // the pointer is shown at its real size on screen
            stage.Resize(Width, Height);
            if (pixels != null) pixels.Dispose();
            pixels = new PixelBuffer(Width, Height);
            PaintPage();
            stage.Reset();
            if (mouse.IsEmpty) mouse = new PointF(Width * 0.5f, Height * 0.55f);
            Step();
        }

        /// <summary>The chosen handwriting font at px, made smaller if the text would not fit 'room'.</summary>
        static Font FitFont(Graphics g, string text, float px, float room)
        {
            var f = Fonts.Get(Settings.TextFont, px);
            float w = g.MeasureString(text, f).Width;
            if (w <= room || w <= 0) return f;
            f.Dispose();
            return Fonts.Get(Settings.TextFont, Math.Max(6, px * room / w));
        }

        void Write(Graphics g, string text, float px, Color c, float x, float y, float room)
        {
            using (var f = FitFont(g, text, px, room)) using (var b = new SolidBrush(c)) g.DrawString(text, f, b, x, y);
        }

        /// <summary>The exercise-book page behind the pointer: a heading, a lesson title and a "10" stamp.</summary>
        void PaintPage()
        {
            if (page != null) page.Dispose();
            page = Paper.Make("oli", Width, Height, stage.RefH * 0.55);
            using (var g = Graphics.FromImage(page))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                float u = Math.Min(Height / 100f, Width / 62f), left = 9 * u, room = Width - left - 4 * u;
                Write(g, "Tập viết: ă â ê", 7.5f * u, Theme.Purple, left, 7 * u, room);
                Write(g, "Bài 5: Phân số", 5.5f * u, Theme.Ink, left, 20 * u, room);
                StampMark.DrawAt(g, "diem10", new PointF(Width - 14 * u, Height - 14 * u), 16 * u, 1);
            }
        }

        void Wake() { if (!ticker.Enabled) ticker.Start(); }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); over = true; PreciseTimer(true); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); over = false; PreciseTimer(false); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            mouse = e.Location;
            if (drawing)
            {
                float dx = mouse.X - pressedAt.X, dy = mouse.Y - pressedAt.Y;
                if (!stage.IsDrawing && dx * dx + dy * dy >= 16) stage.BeginStroke(pressedAt, false);
                if (stage.IsDrawing) stage.ExtendStroke(mouse);
            }
            Wake();
            if (Clock.Now - last >= 1 / 120.0) Step();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            mouse = e.Location;
            stage.Look.Press();
            stage.Look.Tap(mouse);
            app.Sounds.Tap();
            if (e.Button == MouseButtons.Left) { drawing = true; pressedAt = mouse; }
            Wake();
            Step();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left) { drawing = false; stage.EndAll(); }
            stage.Look.Held = false;
            Step();
        }

        void Step()
        {
            if (pixels == null || IsDisposed || !IsHandleCreated) return;
            double now = Clock.Now, dt = now - last;
            last = now;
            var area = stage.Frame(mouse.X, mouse.Y, dt, now);
            if (!area.IsEmpty)
            {
                stage.Paint(pixels.G, area, page);
                using (var g = CreateGraphics()) CopyOut(g, area);
            }
            if (!over && !stage.IsSettling && !stage.Look.Held) ticker.Stop();   // nothing moves: sleep until the mouse comes back
        }

        /// <summary>Copies part of the buffer to the screen with a plain GDI blit.</summary>
        void CopyOut(Graphics g, Rectangle r)
        {
            pixels.G.Flush(FlushIntention.Sync);
            Win32.GdiFlush();
            IntPtr dc = g.GetHdc();
            try { Win32.BitBlt(dc, r.X, r.Y, r.Width, r.Height, pixels.Hdc, r.X, r.Y, Win32.SrcCopy); }
            finally { g.ReleaseHdc(dc); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (pixels == null) { e.Graphics.Clear(Color.White); return; }
            CopyOut(e.Graphics, Rectangle.Intersect(e.ClipRectangle, new Rectangle(0, 0, pixels.W, pixels.H)));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Shutdown();
                ticker.Dispose();
                if (pixels != null) pixels.Dispose();
                if (page != null) page.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
