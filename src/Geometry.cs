using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

namespace PhanTrang
{
    /// <summary>
    /// One geometry instrument on the screen: a ruler, a protractor, a set square (45° or 30-60°) or a compass.
    /// Each lives in its own layered window (drawn by <see cref="ClassArt"/>, a little see-through). Drag it to move,
    /// drag the round knob to turn it (Shift: steps of 15°), the small ✕ knob puts it away. Pen lines started next
    /// to a straight edge follow that edge. The compass: drag the needle to move it, a leg to open or close it, and
    /// the chalk end to draw an arc on the board.
    /// </summary>
    public sealed class GeoTool : IDisposable
    {
        [DllImport("user32.dll")] static extern IntPtr SetCapture(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();

        public readonly string Kind;           // ruler | protractor | set45 | set30 | compass
        public PointF Origin;                  // screen: ruler zero mark, protractor centre, set square right angle, compass needle
        public float Angle;                    // degrees, clockwise on screen
        public PointF Pencil;                  // compass only: the chalk end (screen)
        readonly float cm;                     // pixels per centimetre

        public Action<PointF> StrokeBegin, StrokeTo;   // screen points: the compass draws through these
        public Action StrokeEnd;
        public Action<GeoTool> CloseRequested;
        public Func<bool> ShiftHeld = () => false;

        readonly GlassWindow glass = new GlassWindow(false);
        PixelBuffer buf;
        Point at;                              // the window's top left on screen
        PointF localOrigin;                    // where Origin sits inside the buffer
        bool shown;

        /// <summary>Alpha of the last picture, for hit tests from the mouse hook's thread (replaced whole, never changed).</summary>
        sealed class Mask { public int Left, Top, W, H; public byte[] A; }
        volatile Mask mask;

        enum Grab { None, Move, Rotate, Radius, Draw }
        Grab grab;
        PointF grabFrom, originFrom, pencilFrom;
        float angleFrom, grabAngle, lastArc;
        string label;

        const float Knob = 15;

        public GeoTool(string kind, PointF origin, float pxPerCm)
        {
            Kind = kind;
            Origin = origin;
            cm = Math.Max(10, pxPerCm);
            if (kind == "compass") Pencil = new PointF(origin.X + 4 * cm, origin.Y);
            glass.Received += OnMessage;
        }

        // ---------------- shape ----------------

        float RulerLength { get { return 16 * cm + 2 * cm * 0.25f; } }
        float RulerWidth { get { return 1.7f * cm; } }
        float ProtractorRadius { get { return 5.5f * cm; } }
        float SetLeg { get { return 10 * cm; } }
        float SetHeight { get { return SetLeg * (Kind == "set30" ? 0.577f : 1f); } }

        /// <summary>The outline in local units (before turning), for snapping and the knobs.</summary>
        PointF[] Outline()
        {
            switch (Kind)
            {
                case "ruler": return new[] { new PointF(0, 0), new PointF(RulerLength, 0), new PointF(RulerLength, RulerWidth), new PointF(0, RulerWidth) };
                case "protractor":
                    {
                        float r = ProtractorRadius;
                        var pts = new List<PointF> { new PointF(-r, 0) };
                        for (int a = 180; a <= 360; a += 6) pts.Add(new PointF((float)(r * Math.Cos(a * Math.PI / 180)), (float)(r * Math.Sin(a * Math.PI / 180))));
                        return pts.ToArray();
                    }
                case "set45":
                case "set30": return new[] { new PointF(0, 0), new PointF(SetLeg, 0), new PointF(0, -SetHeight) };
            }
            return new PointF[0];
        }

        PointF RotateKnob
        {
            get
            {
                switch (Kind)
                {
                    case "ruler": return new PointF(RulerLength + Knob + 6, RulerWidth / 2);
                    case "protractor": return new PointF(ProtractorRadius + Knob + 6, -Knob);
                    default: return new PointF(SetLeg + Knob + 6, -Knob);
                }
            }
        }

        PointF CloseKnobLocal
        {
            get
            {
                switch (Kind)
                {
                    case "ruler": return new PointF(-Knob - 6, RulerWidth / 2);
                    case "protractor": return new PointF(-ProtractorRadius - Knob - 6, -Knob);
                    case "compass": return new PointF(0, Knob * 2.4f);
                    default: return new PointF(-Knob - 6, Knob + 4);
                }
            }
        }

        PointF ToScreen(PointF p)
        {
            if (Kind == "compass") return new PointF(Origin.X + p.X, Origin.Y + p.Y);
            double a = Angle * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
            return new PointF((float)(Origin.X + p.X * c - p.Y * s), (float)(Origin.Y + p.X * s + p.Y * c));
        }

        static float Dist(PointF a, PointF b) { float dx = a.X - b.X, dy = a.Y - b.Y; return (float)Math.Sqrt(dx * dx + dy * dy); }

        /// <summary>The compass hinge, the same way ClassArt places it.</summary>
        static PointF Hinge(PointF pivot, PointF pencil)
        {
            float dx = pencil.X - pivot.X, dy = pencil.Y - pivot.Y, d = (float)Math.Sqrt(dx * dx + dy * dy);
            float ux = d > 0.5f ? dx / d : 1, uy = d > 0.5f ? dy / d : 0, nx = -uy, ny = ux;
            if (ny > 0 || (Math.Abs(ny) < 1e-4f && nx > 0)) { nx = -nx; ny = -ny; }
            float lg = Math.Max(150, d * 0.58f), hh = (float)Math.Sqrt(Math.Max(0, lg * lg - d * d / 4));
            return new PointF((pivot.X + pencil.X) / 2 + nx * hh, (pivot.Y + pencil.Y) / 2 + ny * hh);
        }

        /// <summary>Straight edges on screen that a pen line can follow.</summary>
        public IEnumerable<PointF[]> Edges()
        {
            var o = Outline();
            if (Kind == "protractor") { yield return new[] { ToScreen(new PointF(-ProtractorRadius, 0)), ToScreen(new PointF(ProtractorRadius, 0)) }; yield break; }
            for (int i = 0; i < o.Length; i++) yield return new[] { ToScreen(o[i]), ToScreen(o[(i + 1) % o.Length]) };
        }

        /// <summary>
        /// If 'start' is within 'reach' pixels of a straight edge, puts 'p' on the line through 'start' along that
        /// edge (so the line runs beside the instrument) and returns true.
        /// </summary>
        public bool Snap(PointF start, ref PointF p, float reach)
        {
            foreach (var e in Edges())
            {
                float ex = e[1].X - e[0].X, ey = e[1].Y - e[0].Y, len = (float)Math.Sqrt(ex * ex + ey * ey);
                if (len < 1) continue;
                ex /= len; ey /= len;
                float sx = start.X - e[0].X, sy = start.Y - e[0].Y;
                float along = sx * ex + sy * ey, across = Math.Abs(sx * -ey + sy * ex);
                if (across > reach || along < -reach || along > len + reach) continue;
                float t = (p.X - start.X) * ex + (p.Y - start.Y) * ey;
                p = new PointF(start.X + t * ex, start.Y + t * ey);
                return true;
            }
            return false;
        }

        /// <summary>Whether a screen point is on the instrument (safe from any thread).</summary>
        public bool Hit(int x, int y)
        {
            var m = mask;
            if (m == null || !shown) return false;
            int bx = x - m.Left, by = y - m.Top;
            return bx >= 0 && by >= 0 && bx < m.W && by < m.H && m.A[by * m.W + bx] > 24;
        }

        // ---------------- drawing ----------------

        /// <summary>Draws the instrument again (after turning, resizing or while the compass moves) and shows it.</summary>
        public void Render()
        {
            RectangleF box;
            if (Kind == "compass")
            {
                var h = Hinge(Origin, Pencil);
                float l = Math.Min(Math.Min(Origin.X, Pencil.X), h.X), t = Math.Min(Math.Min(Origin.Y, Pencil.Y), h.Y);
                float r = Math.Max(Math.Max(Origin.X, Pencil.X), h.X), b = Math.Max(Math.Max(Origin.Y, Pencil.Y), h.Y);
                box = RectangleF.FromLTRB(l, t, r, Math.Max(b, ToScreen(CloseKnobLocal).Y + Knob));
                box.Inflate(48, 48);
            }
            else
            {
                var pts = Outline().Concat(new[] { RotateKnob, CloseKnobLocal }).Select(ToScreen).ToList();
                box = RectangleF.FromLTRB(pts.Min(p => p.X), pts.Min(p => p.Y), pts.Max(p => p.X), pts.Max(p => p.Y));
                box.Inflate(Knob + 24, Knob + 24);
            }
            int w = (int)Math.Ceiling(box.Width), hgt = (int)Math.Ceiling(box.Height);
            if (buf == null || buf.W != w || buf.H != hgt) { if (buf != null) buf.Dispose(); buf = new PixelBuffer(w, hgt); }
            at = new Point((int)Math.Floor(box.X), (int)Math.Floor(box.Y));
            localOrigin = new PointF(Origin.X - at.X, Origin.Y - at.Y);

            var g = buf.G;
            g.ResetTransform();
            g.CompositingMode = CompositingMode.SourceCopy;
            g.Clear(Color.Transparent);
            g.CompositingMode = CompositingMode.SourceOver;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            if (Kind == "compass")
            {
                g.TranslateTransform(-at.X, -at.Y);
                ClassArt.PaintCompass(g, Origin, Pencil, grab == Grab.Draw);
                g.ResetTransform();
                g.TranslateTransform(localOrigin.X, localOrigin.Y);
            }
            else
            {
                g.TranslateTransform(localOrigin.X, localOrigin.Y);
                g.RotateTransform(Angle);
                if (Kind == "ruler") ClassArt.PaintRuler(g, RulerLength, RulerWidth, cm);
                else if (Kind == "protractor") ClassArt.PaintProtractor(g, ProtractorRadius);
                else ClassArt.PaintSetSquare(g, SetLeg, cm, Kind == "set30" ? "30" : "45");
                PaintKnob(g, RotateKnob, "rotate");
            }
            PaintKnob(g, CloseKnobLocal, "close");
            g.ResetTransform();
            if (label != null) PaintLabel(g);

            TakeMask();
            Present();
        }

        void PaintKnob(Graphics g, PointF c, string kind)
        {
            var r = new RectangleF(c.X - Knob, c.Y - Knob, 2 * Knob, 2 * Knob);
            using (var shade = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillEllipse(shade, r.X + 1.5f, r.Y + 2.5f, r.Width, r.Height);
            using (var fill = new SolidBrush(kind == "close" ? Color.FromArgb(235, 251, 248, 239) : Color.FromArgb(240, Theme.Yellow))) g.FillEllipse(fill, r);
            using (var edge = new Pen(Theme.Green, 1.6f)) g.DrawEllipse(edge, r);
            using (var pen = new Pen(kind == "close" ? Theme.Red : Theme.Green, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                if (kind == "close")
                {
                    float s = Knob * 0.38f;
                    g.DrawLine(pen, c.X - s, c.Y - s, c.X + s, c.Y + s);
                    g.DrawLine(pen, c.X + s, c.Y - s, c.X - s, c.Y + s);
                }
                else
                {
                    float s = Knob * 0.55f;
                    g.DrawArc(pen, c.X - s, c.Y - s, 2 * s, 2 * s, 200, 250);
                    var tip = new PointF(c.X + s * (float)Math.Cos(90 * Math.PI / 180), c.Y + s * (float)Math.Sin(90 * Math.PI / 180));
                    g.DrawLines(pen, new[] { new PointF(tip.X - 4, tip.Y - 3.5f), tip, new PointF(tip.X - 4.5f, tip.Y + 3.5f) });
                }
            }
        }

        /// <summary>The angle while turning, the radius while opening the compass.</summary>
        void PaintLabel(Graphics g)
        {
            var anchor = Kind == "compass" ? new PointF(Pencil.X - at.X, Pencil.Y - at.Y + 34) : new PointF(localOrigin.X, localOrigin.Y - 30);
            using (var f = Fonts.Get("uiBold", 16))
            {
                var size = g.MeasureString(label, f);
                var r = new RectangleF(anchor.X - size.Width / 2 - 10, anchor.Y - size.Height / 2 - 4, size.Width + 20, size.Height + 8);
                r.X = Math.Max(2, Math.Min(buf.W - r.Width - 2, r.X));
                r.Y = Math.Max(2, Math.Min(buf.H - r.Height - 2, r.Y));
                using (var p = Draw.Round(r, r.Height / 2)) using (var b = new SolidBrush(Color.FromArgb(220, Theme.Ink))) g.FillPath(b, p);
                using (var white = new SolidBrush(Color.White)) g.DrawString(label, f, white, r.X + 10, r.Y + 4);
            }
        }

        void TakeMask()
        {
            var bits = buf.Bmp.LockBits(new Rectangle(0, 0, buf.W, buf.H), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                var row = new byte[bits.Stride];
                var a = new byte[buf.W * buf.H];
                for (int y = 0; y < buf.H; y++)
                {
                    Marshal.Copy(bits.Scan0 + y * bits.Stride, row, 0, bits.Stride);
                    for (int x = 0; x < buf.W; x++) a[y * buf.W + x] = row[x * 4 + 3];
                }
                mask = new Mask { Left = at.X, Top = at.Y, W = buf.W, H = buf.H, A = a };
            }
            finally { buf.Bmp.UnlockBits(bits); }
        }

        void Present()
        {
            if (buf == null) return;
            buf.Show(glass.Handle, at.X, at.Y);
            if (!shown) { glass.Show(); shown = true; }
        }

        /// <summary>Moves without drawing again (only the window and the hit mask move).</summary>
        void MoveBy(float dx, float dy)
        {
            Origin = new PointF(Origin.X + dx, Origin.Y + dy);
            if (Kind == "compass") Pencil = new PointF(Pencil.X + dx, Pencil.Y + dy);
            Render();
        }

        public void Show() { Render(); }
        public void Hide() { glass.Hide(); shown = false; }
        public void Raise() { if (shown) glass.Raise(); }
        public bool Visible { get { return shown; } }

        // ---------------- mouse ----------------

        void OnMessage(System.Windows.Forms.Message m)
        {
            var p = Win32.CursorPos();
            var pt = new PointF(p.X, p.Y);
            switch (m.Msg)
            {
                case Win32.Wm.LButtonDown: Down(pt); break;
                case Win32.Wm.MouseMove: if (grab != Grab.None) MoveTo(pt); break;
                case Win32.Wm.LButtonUp: Up(); break;
                case 0x215: /* WM_CAPTURECHANGED */ if (grab != Grab.None) Up(); break;
            }
        }

        void Down(PointF p)
        {
            if (Dist(p, ToScreen(CloseKnobLocal)) <= Knob + 3) { if (CloseRequested != null) CloseRequested(this); return; }
            grabFrom = p; originFrom = Origin; pencilFrom = Pencil; angleFrom = Angle;
            if (Kind == "compass")
            {
                if (Dist(p, Pencil) <= 30)
                {
                    grab = Grab.Draw;
                    lastArc = (float)Math.Atan2(Pencil.Y - Origin.Y, Pencil.X - Origin.X);
                    if (StrokeBegin != null) StrokeBegin(Pencil);
                }
                else grab = Dist(p, Origin) <= 34 ? Grab.Move : Grab.Radius;
            }
            else if (Dist(p, ToScreen(RotateKnob)) <= Knob + 4)
            {
                grab = Grab.Rotate;
                grabAngle = (float)(Math.Atan2(p.Y - Origin.Y, p.X - Origin.X) * 180 / Math.PI);
            }
            else grab = Grab.Move;
            SetCapture(glass.Handle);
            if (grab != Grab.Move) Render();
        }

        void MoveTo(PointF p)
        {
            switch (grab)
            {
                case Grab.Move:
                    MoveBy(originFrom.X + (p.X - grabFrom.X) - Origin.X, originFrom.Y + (p.Y - grabFrom.Y) - Origin.Y);
                    break;
                case Grab.Rotate:
                    {
                        float now = (float)(Math.Atan2(p.Y - Origin.Y, p.X - Origin.X) * 180 / Math.PI);
                        float a = angleFrom + (now - grabAngle);
                        a = ((a % 360) + 360) % 360;
                        if (ShiftHeld()) a = (float)(Math.Round(a / 15) * 15) % 360;
                        else if (Math.Abs(a - Math.Round(a / 90) * 90) < 1.5) a = (float)(Math.Round(a / 90) * 90) % 360;   // a soft stop at level and upright
                        Angle = a;
                        float shown = (360 - a) % 360;   // as on a protractor: counter-clockwise from level
                        label = Math.Round(shown > 180 ? shown - 360 : shown).ToString("0") + "°";
                        Render();
                        break;
                    }
                case Grab.Radius:
                    {
                        float dx = pencilFrom.X - Origin.X, dy = pencilFrom.Y - Origin.Y, d = Math.Max(1, (float)Math.Sqrt(dx * dx + dy * dy));
                        float want = Dist(p, Origin) - Dist(grabFrom, Origin) + d;
                        want = Math.Max(0.5f * cm, Math.Min(14 * cm, want));
                        Pencil = new PointF(Origin.X + dx / d * want, Origin.Y + dy / d * want);
                        label = "r = " + (want / cm).ToString("0.0") + " cm";
                        Render();
                        break;
                    }
                case Grab.Draw:
                    {
                        float r = Dist(Pencil, Origin);
                        float target = (float)Math.Atan2(p.Y - Origin.Y, p.X - Origin.X);
                        float delta = target - lastArc;
                        while (delta > Math.PI) delta -= (float)(2 * Math.PI);
                        while (delta < -Math.PI) delta += (float)(2 * Math.PI);
                        int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(delta) / (2 * Math.PI / 180)));
                        for (int i = 1; i <= steps; i++)
                        {
                            float a = lastArc + delta * i / steps;
                            var q = new PointF(Origin.X + r * (float)Math.Cos(a), Origin.Y + r * (float)Math.Sin(a));
                            if (StrokeTo != null) StrokeTo(q);
                        }
                        lastArc += delta;
                        Pencil = new PointF(Origin.X + r * (float)Math.Cos(lastArc), Origin.Y + r * (float)Math.Sin(lastArc));
                        Render();
                        break;
                    }
            }
        }

        void Up()
        {
            var was = grab;
            grab = Grab.None;
            label = null;
            ReleaseCapture();
            if (was == Grab.Draw && StrokeEnd != null) StrokeEnd();
            if (was != Grab.None && was != Grab.Move) Render();
        }

        public void Dispose()
        {
            glass.Hide();
            glass.DestroyHandle();
            if (buf != null) buf.Dispose();
            mask = null;
        }
    }

    /// <summary>The instruments on the screen; at most one of each kind.</summary>
    public sealed class GeoTools
    {
        readonly List<GeoTool> tools = new List<GeoTool>();
        volatile GeoTool[] snapshot = new GeoTool[0];

        public Action<PointF> StrokeBegin, StrokeTo;
        public Action StrokeEnd;
        public Func<bool> ShiftHeld = () => false;
        public event Action Changed;

        public bool Has(string kind) { return tools.Any(t => t.Kind == kind); }
        public bool Any { get { return tools.Count > 0; } }

        /// <summary>Puts the instrument out (in the middle of 'area', sized for a screen 'screenH' pixels tall), or away if it is out.</summary>
        public void Toggle(string kind, RECT area, int screenH)
        {
            var old = tools.FirstOrDefault(t => t.Kind == kind);
            if (old != null) { Remove(old); return; }
            float cm = Math.Max(24, screenH / 20f);
            var mid = new PointF(area.Left + area.Width / 2f, area.Top + area.Height / 2f);
            PointF origin;
            switch (kind)
            {
                case "ruler": origin = new PointF(mid.X - 8 * cm, mid.Y - 0.85f * cm); break;
                case "protractor": origin = new PointF(mid.X, mid.Y + 2.5f * cm); break;
                case "compass": origin = new PointF(mid.X - 2 * cm, mid.Y + 2 * cm); break;
                default: origin = new PointF(mid.X - 5 * cm, mid.Y + 4 * cm); break;
            }
            var tool = new GeoTool(kind, origin, cm)
            {
                StrokeBegin = p => { if (StrokeBegin != null) StrokeBegin(p); },
                StrokeTo = p => { if (StrokeTo != null) StrokeTo(p); },
                StrokeEnd = () => { if (StrokeEnd != null) StrokeEnd(); },
                CloseRequested = t => Remove(t),
                ShiftHeld = () => ShiftHeld(),
            };
            tools.Add(tool);
            snapshot = tools.ToArray();
            tool.Show();
            if (Changed != null) Changed();
        }

        void Remove(GeoTool t)
        {
            tools.Remove(t);
            snapshot = tools.ToArray();
            t.Dispose();
            if (Changed != null) Changed();
        }

        public void HideAll() { foreach (var t in tools.ToList()) Remove(t); }

        /// <summary>Whether a screen point is on one of the instruments (called from the mouse hook's thread).</summary>
        public bool Hit(int x, int y)
        {
            foreach (var t in snapshot) if (t.Hit(x, y)) return true;
            return false;
        }

        /// <summary>A pen line starting next to a straight edge follows it: moves p onto that line.</summary>
        public bool Snap(PointF start, ref PointF p)
        {
            foreach (var t in tools) if (t.Kind != "compass" && t.Snap(start, ref p, 26)) return true;
            return false;
        }

        public void Raise() { foreach (var t in tools) t.Raise(); }
    }
}
