using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace PhanTrang
{
    /// <summary>
    /// A mark left on the screen or on a board sheet: a pen line, a shape, typed text, a marking stamp or a picture.
    /// Times are <see cref="Clock.Now"/> seconds. Done stays -1 while the teacher is still drawing it.
    /// </summary>
    public abstract class Mark
    {
        public Color Color;
        public double Start;
        public double Done = -1;
        public float Alpha = 1;
        public RectangleF Bounds;
        /// <summary>"Vẽ tạm": disappears by itself and never enters the undo history or a saved lesson.</summary>
        public bool Temporary;

        public bool Finished { get { return Done >= 0; } }

        public abstract void Draw(Graphics g, double now);
        /// <summary>Whether p lies within r pixels of the visible mark.</summary>
        public abstract bool Hit(PointF p, float r);
        /// <summary>True while the mark still changes by itself (a stamp popping in).</summary>
        public virtual bool Animating(double now) { return false; }
    }

    /// <summary>Small geometry and pen helpers shared by the marks.</summary>
    static class Geo
    {
        public static Color WithAlpha(Color c, float k)
        {
            int a = (int)(c.A * k);
            return Color.FromArgb(a < 0 ? 0 : a > 255 ? 255 : a, c.R, c.G, c.B);
        }

        /// <summary>Squared distance from p to the segment a–b.</summary>
        public static float SegmentDist2(PointF p, PointF a, PointF b)
        {
            float vx = b.X - a.X, vy = b.Y - a.Y, wx = p.X - a.X, wy = p.Y - a.Y;
            float len2 = vx * vx + vy * vy;
            float t = len2 > 1e-6f ? (wx * vx + wy * vy) / len2 : 0;
            if (t < 0) t = 0; else if (t > 1) t = 1;
            float ex = wx - t * vx, ey = wy - t * vy;
            return ex * ex + ey * ey;
        }

        public static bool NearLine(IList<PointF> pts, PointF p, float r)
        {
            float r2 = r * r;
            if (pts.Count == 1) return SegmentDist2(p, pts[0], pts[0]) <= r2;
            for (int i = 0; i + 1 < pts.Count; i++)
                if (SegmentDist2(p, pts[i], pts[i + 1]) <= r2) return true;
            return false;
        }

        public static RectangleF Box(IEnumerable<PointF> pts, float pad)
        {
            bool any = false;
            float l = 0, t = 0, r = 0, b = 0;
            foreach (var q in pts)
            {
                if (!any) { l = r = q.X; t = b = q.Y; any = true; continue; }
                if (q.X < l) l = q.X; else if (q.X > r) r = q.X;
                if (q.Y < t) t = q.Y; else if (q.Y > b) b = q.Y;
            }
            return any ? new RectangleF(l - pad, t - pad, r - l + 2 * pad, b - t + 2 * pad) : RectangleF.Empty;
        }

        public static Pen RoundPen(Color c, float width)
        {
            return new Pen(c, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        }

        /// <summary>Ink with a faint drop shadow underneath, so it reads on busy slides.</summary>
        public static void Ink(Graphics g, GraphicsPath path, Color c, float width, float alpha)
        {
            var saved = g.Save();
            g.TranslateTransform(1f, 1.5f);
            using (var shade = RoundPen(Color.FromArgb((int)(46 * alpha), 0, 0, 0), width + 1)) g.DrawPath(shade, path);
            g.Restore(saved);
            using (var pen = RoundPen(WithAlpha(c, alpha), width)) g.DrawPath(pen, path);
        }
    }

    /// <summary>A free-hand pen or highlighter line. The outline is a smooth spline through the sampled points.</summary>
    public class PenMark : Mark
    {
        public readonly List<PointF> Pts = new List<PointF>();
        public float Width;
        public bool Highlighter;
        GraphicsPath shape;      // built lazily; dropped whenever points are added
        float minX, minY, maxX, maxY;

        float LineWidth { get { return Highlighter ? Width * 3.4f : Width; } }

        public void Add(PointF p)
        {
            if (Pts.Count == 0) { minX = maxX = p.X; minY = maxY = p.Y; }
            else { minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X); minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y); }
            Pts.Add(p);
            float pad = LineWidth + 6;
            Bounds = RectangleF.FromLTRB(minX - pad, minY - pad, maxX + pad, maxY + pad);
            if (shape != null) { shape.Dispose(); shape = null; }
        }

        public void AddRange(IEnumerable<PointF> pts) { foreach (var p in pts) Add(p); }

        GraphicsPath Shape()
        {
            if (shape != null) return shape;
            shape = new GraphicsPath();
            if (Pts.Count == 1) shape.AddLine(Pts[0], new PointF(Pts[0].X + 0.01f, Pts[0].Y));
            else if (Pts.Count == 2) shape.AddLine(Pts[0], Pts[1]);
            else if (Pts.Count > 2) shape.AddCurve(Pts.ToArray(), 0.45f);
            return shape;
        }

        public override void Draw(Graphics g, double now)
        {
            if (Pts.Count == 0) return;
            if (!Highlighter) { Geo.Ink(g, Shape(), Color, Width, Alpha); return; }
            using (var pen = Geo.RoundPen(Geo.WithAlpha(Color, 0.38f * Alpha), LineWidth)) g.DrawPath(pen, Shape());
        }

        public override bool Hit(PointF p, float r) { return Geo.NearLine(Pts, p, r + LineWidth / 2); }
    }

    /// <summary>A straight line, an arrow, a box or an ellipse between two corner points.</summary>
    public class ShapeMark : Mark
    {
        public string Kind;
        public PointF P0, P1;
        public float Width;

        float HeadLength { get { return Math.Max(14, Width * 4.2f); } }

        RectangleF Frame
        {
            get { return RectangleF.FromLTRB(Math.Min(P0.X, P1.X), Math.Min(P0.Y, P1.Y), Math.Max(P0.X, P1.X), Math.Max(P0.Y, P1.Y)); }
        }

        public void SetEnd(PointF p)
        {
            P1 = p;
            Bounds = Geo.Box(new[] { P0, P1 }, Width + HeadLength + 6);
        }

        /// <summary>Points along the visible outline, for hit testing.</summary>
        PointF[] Trace()
        {
            var f = Frame;
            if (Kind == "rect")
                return new[] { new PointF(f.Left, f.Top), new PointF(f.Right, f.Top), new PointF(f.Right, f.Bottom), new PointF(f.Left, f.Bottom), new PointF(f.Left, f.Top) };
            if (Kind == "ellipse")
            {
                const int n = 48;
                var ring = new PointF[n + 1];
                float cx = f.X + f.Width / 2, cy = f.Y + f.Height / 2;
                for (int i = 0; i <= n; i++)
                {
                    double t = 2 * Math.PI * i / n;
                    ring[i] = new PointF(cx + (float)(Math.Cos(t) * f.Width / 2), cy + (float)(Math.Sin(t) * f.Height / 2));
                }
                return ring;
            }
            return new[] { P0, P1 };
        }

        public override void Draw(Graphics g, double now)
        {
            var f = Frame;
            using (var outline = new GraphicsPath())
            {
                switch (Kind)
                {
                    case "rect": outline.AddRectangle(new RectangleF(f.X, f.Y, Math.Max(1, f.Width), Math.Max(1, f.Height))); break;
                    case "ellipse": outline.AddEllipse(f.X, f.Y, Math.Max(1, f.Width), Math.Max(1, f.Height)); break;
                    case "arrow": if (DrawArrow(g)) return; outline.AddLine(P0, P1); break;
                    default: outline.AddLine(P0, P1); break;
                }
                Geo.Ink(g, outline, Color, Width, Alpha);
            }
        }

        /// <summary>The shaft stops where the filled head begins. False if the arrow is too short to have a head.</summary>
        bool DrawArrow(Graphics g)
        {
            float dx = P1.X - P0.X, dy = P1.Y - P0.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1) return false;
            float ux = dx / len, uy = dy / len;
            float head = Math.Min(HeadLength, len * 0.6f), half = head * 0.6f;
            var neck = new PointF(P1.X - ux * head, P1.Y - uy * head);
            using (var shaft = new GraphicsPath())
            {
                shaft.AddLine(P0, neck);
                Geo.Ink(g, shaft, Color, Width, Alpha);
            }
            var tip = new[] { P1, new PointF(neck.X - uy * half, neck.Y + ux * half), new PointF(neck.X + uy * half, neck.Y - ux * half) };
            var c = Geo.WithAlpha(Color, Alpha);
            using (var fill = new SolidBrush(c)) g.FillPolygon(fill, tip);
            using (var edge = new Pen(c, Width * 0.6f) { LineJoin = LineJoin.Round }) g.DrawPolygon(edge, tip);
            return true;
        }

        public override bool Hit(PointF p, float r) { return Geo.NearLine(Trace(), p, r + Width / 2); }

        /// <summary>With Shift held: lines snap to 45° steps, boxes and ellipses become squares and circles.</summary>
        public static PointF Constrain(string kind, PointF from, PointF to)
        {
            float dx = to.X - from.X, dy = to.Y - from.Y;
            if (kind == "rect" || kind == "ellipse")
            {
                float side = Math.Max(Math.Abs(dx), Math.Abs(dy));
                return new PointF(from.X + (dx < 0 ? -side : side), from.Y + (dy < 0 ? -side : side));
            }
            const double step = Math.PI / 4;
            double angle = Math.Round(Math.Atan2(dy, dx) / step) * step, len = Math.Sqrt(dx * dx + dy * dy);
            return new PointF(from.X + (float)(len * Math.Cos(angle)), from.Y + (float)(len * Math.Sin(angle)));
        }
    }

    /// <summary>One typed line in a handwriting font, drawn as an outline with a soft halo.</summary>
    public class TextMark : Mark
    {
        public string Text = "";
        public string FontId = "mali";
        public float Px;
        public PointF Pos;
        /// <summary>The line still being typed: a caret blinks after it.</summary>
        public bool ShowCaret;

        GraphicsPath outline;
        float advance;

        public float LineHeight { get { return Px * 1.3f; } }

        /// <summary>Rebuilds the outline and bounds. Call after changing the text, font, size or position.</summary>
        public void Layout()
        {
            if (outline != null) outline.Dispose();
            outline = TextRuns.Build(Text, FontId, Px, Pos, out advance);
            float bottom = Pos.Y + LineHeight;
            if (outline.PointCount > 0) bottom = Math.Max(bottom, outline.GetBounds().Bottom);
            float right = Pos.X + Math.Max(advance, Px * 0.6f);
            Bounds = RectangleF.FromLTRB(Pos.X - 8 - Px * 0.1f, Pos.Y - 8 - Px * 0.2f, right + 20 + Px * 0.2f, bottom + 8);
        }

        public override void Draw(Graphics g, double now)
        {
            if (outline == null) Layout();
            if (outline.PointCount > 0)
            {
                bool darkInk = Color.R * 299 + Color.G * 587 + Color.B * 114 < 140000;
                var haloColor = darkInk ? Color.FromArgb((int)(170 * Alpha), 255, 255, 255) : Color.FromArgb((int)(90 * Alpha), 0, 0, 0);
                using (var halo = new Pen(haloColor, Math.Max(2, Px / 10f)) { LineJoin = LineJoin.Round }) g.DrawPath(halo, outline);
                using (var fill = new SolidBrush(Geo.WithAlpha(Color, Alpha))) g.FillPath(fill, outline);
            }
            bool blinkOn = ((long)(now * 2) & 1) == 0;
            if (ShowCaret && blinkOn)
                using (var caret = new SolidBrush(Theme.Yellow))
                    g.FillRectangle(caret, Pos.X + advance + Px * 0.08f, Pos.Y + Px * 0.12f, Math.Max(2.5f, Px * 0.06f), Px * 1.05f);
        }

        public override bool Hit(PointF p, float r) { return RectangleF.Inflate(Bounds, r, r).Contains(p); }
    }

    /// <summary>
    /// Text as a vector outline. Characters the chosen handwriting font does not have (√, ≤, π, ∠ …)
    /// are taken from a Windows symbol font and lined up on the same baseline.
    /// </summary>
    public static class TextRuns
    {
        static readonly Bitmap measureBmp = new Bitmap(1, 1);
        static readonly StringFormat fmt = MakeFormat();

        static StringFormat MakeFormat()
        {
            var f = (StringFormat)StringFormat.GenericTypographic.Clone();
            f.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
            return f;
        }

        static float Ascent(FontFamily f, FontStyle s, float px) { return px * f.GetCellAscent(s) / f.GetEmHeight(s); }

        /// <summary>-1 = main font, otherwise index into Fonts.Fallbacks.</summary>
        static int Pick(FontFamily main, char c, int prev)
        {
            if (c >= 0x300 && c <= 0x36F) return prev;   // combining accent stays with its letter
            if (Fonts.Covers(main, c)) return -1;
            var fb = Fonts.Fallbacks;
            for (int i = 0; i < fb.Length; i++) if (Fonts.Covers(fb[i], c)) return i;
            return -1;
        }

        public static GraphicsPath Build(string text, string fontId, float px, PointF pos, out float width)
        {
            var path = new GraphicsPath();
            width = 0;
            if (string.IsNullOrEmpty(text)) return path;
            var main = Fonts.Family(fontId);
            var mainStyle = Fonts.StyleFor(main);
            float mainAsc = Ascent(main, mainStyle, px);
            float x = pos.X;
            using (var g = Graphics.FromImage(measureBmp))
            {
                int i = 0, prev = -1;
                while (i < text.Length)
                {
                    int which = Pick(main, text[i], prev);
                    int j = i + 1;
                    while (j < text.Length && Pick(main, text[j], which) == which) j++;
                    string run = text.Substring(i, j - i);
                    var fam = which < 0 ? main : Fonts.Fallbacks[which];
                    var style = which < 0 ? mainStyle : Fonts.StyleFor(fam);
                    float y = pos.Y + (which < 0 ? 0 : mainAsc - Ascent(fam, style, px));
                    path.AddString(run, fam, (int)style, px, new PointF(x, y), fmt);
                    using (var f = new Font(fam, px, style, GraphicsUnit.Pixel))
                        x += g.MeasureString(run, f, PointF.Empty, fmt).Width;
                    prev = which;
                    i = j;
                }
            }
            width = x - pos.X;
            return path;
        }
    }

    /// <summary>Marking stamps: Điểm 10, Hoa điểm tốt, Tốt lắm!, đúng, sai, ngôi sao. They pop in.</summary>
    public class StampMark : Mark
    {
        public string Kind;
        public PointF Center;
        public float Size;
        const double PopTime = 0.3;

        bool Wide { get { return Kind == "totlam"; } }

        public void Layout()
        {
            float hw = Size * (Wide ? 1.25f : 0.62f) * 1.45f, hh = Size * (Wide ? 0.5f : 0.62f) * 1.45f;
            Bounds = RectangleF.FromLTRB(Center.X - hw - 8, Center.Y - hh - 8, Center.X + hw + 10, Center.Y + hh + 12);
        }

        public override bool Animating(double now) { return now - Start < PopTime + 0.05; }

        /// <summary>Starts a little big and settles: an ease-out overshoot over PopTime.</summary>
        float PopScale(double now)
        {
            double t = Math.Max(0, Math.Min(1, (now - Start) / PopTime));
            double settle = 1 + 0.42 * Math.Pow(1 - t, 3), grow = Math.Min(1, 0.4 + 3 * t);
            return (float)(settle * grow);
        }

        public override void Draw(Graphics g, double now) { DrawAt(g, Kind, Center, Size * PopScale(now), Alpha); }

        public override bool Hit(PointF p, float r) { return RectangleF.Inflate(Bounds, r - 8, r - 8).Contains(p); }

        /// <summary>Draws a stamp centred on c, s pixels across (the "Tốt lắm!" label is wider).</summary>
        public static void DrawAt(Graphics g, string kind, PointF c, float s, float a)
        {
            var st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(c.X, c.Y);
            switch (kind)
            {
                case "diem10": Diem10(g, s, a); break;
                case "hoa": Hoa(g, s, a); break;
                case "totlam": TotLam(g, s, a); break;
                case "dung": Tick(g, Theme.Red, s * 0.15f, a, new PointF(-s * 0.34f, s * 0.02f), new PointF(-s * 0.1f, s * 0.27f), new PointF(s * 0.36f, -s * 0.26f)); break;
                case "sai":
                    Tick(g, Theme.Red, s * 0.14f, a, new PointF(-s * 0.27f, -s * 0.27f), new PointF(s * 0.27f, s * 0.27f));
                    Tick(g, Theme.Red, s * 0.14f, a, new PointF(s * 0.27f, -s * 0.27f), new PointF(-s * 0.27f, s * 0.27f));
                    break;
                default: Sao(g, s, a); break;
            }
            g.Restore(st);
        }

        static Color Al(int alpha, Color c, float a) { return Color.FromArgb((int)(alpha * a), c); }

        static void Shadow(Graphics g, GraphicsPath p, float s, float a)
        {
            using (var sh = new SolidBrush(Color.FromArgb((int)(55 * a), 0, 0, 0)))
            {
                var st = g.Save();
                g.TranslateTransform(s * 0.025f, s * 0.04f);
                g.FillPath(sh, p);
                g.Restore(st);
            }
        }

        static void Tick(Graphics g, Color c, float w, float a, params PointF[] pts)
        {
            var st = g.Save();
            g.TranslateTransform(w * 0.15f, w * 0.25f);
            using (var shade = Geo.RoundPen(Color.FromArgb((int)(55 * a), 0, 0, 0), w + 1)) g.DrawLines(shade, pts);
            g.Restore(st);
            using (var halo = Geo.RoundPen(Al(235, Color.White, a), w * 1.55f)) g.DrawLines(halo, pts);
            using (var pen = Geo.RoundPen(Al(255, c, a), w)) g.DrawLines(pen, pts);
        }

        static void Diem10(Graphics g, float s, float a)
        {
            float r = s * 0.48f;
            using (var p = new GraphicsPath())
            {
                p.AddEllipse(-r, -r, 2 * r, 2 * r);
                Shadow(g, p, s, a);
                using (var b = new SolidBrush(Al(250, Color.White, a))) g.FillPath(b, p);
            }
            using (var ring = new Pen(Al(255, Theme.Red, a), s * 0.045f)) g.DrawEllipse(ring, -r * 0.94f, -r * 0.94f, r * 1.88f, r * 1.88f);
            using (var ring = new Pen(Al(255, Theme.Red, a), s * 0.02f)) g.DrawEllipse(ring, -r * 0.8f, -r * 0.8f, r * 1.6f, r * 1.6f);
            using (var f = Fonts.Get("mali", s * 0.44f))
            using (var small = Fonts.Get("uiBold", s * 0.1f))
            using (var b = new SolidBrush(Al(255, Theme.Red, a)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.DrawString("10", f, b, new PointF(0, s * 0.05f), sf);
                g.DrawString("ĐIỂM", small, b, new PointF(0, -s * 0.25f), sf);
            }
        }

        static void Hoa(Graphics g, float s, float a)
        {
            using (var p = new GraphicsPath())
            {
                for (int i = 0; i < 5; i++)
                {
                    using (var petal = new GraphicsPath())
                    {
                        petal.AddEllipse(-s * 0.15f, -s * 0.47f, s * 0.3f, s * 0.38f);
                        using (var m = new Matrix()) { m.Rotate(i * 72); petal.Transform(m); }
                        p.AddPath(petal, false);
                    }
                }
                Shadow(g, p, s, a);
                using (var halo = new Pen(Al(240, Color.White, a), s * 0.05f)) g.DrawPath(halo, p);
                using (var b = new SolidBrush(Al(255, Color.FromArgb(229, 57, 53), a))) g.FillPath(b, p);
            }
            using (var b = new SolidBrush(Al(255, Color.FromArgb(255, 213, 79), a))) g.FillEllipse(b, -s * 0.15f, -s * 0.15f, s * 0.3f, s * 0.3f);
            using (var e = new Pen(Al(255, Color.FromArgb(214, 160, 30), a), s * 0.015f)) g.DrawEllipse(e, -s * 0.15f, -s * 0.15f, s * 0.3f, s * 0.3f);
        }

        static void TotLam(Graphics g, float s, float a)
        {
            g.RotateTransform(-6);
            var r = new RectangleF(-s * 1.2f, -s * 0.42f, s * 2.4f, s * 0.84f);
            using (var p = new GraphicsPath())
            {
                p.AddRectangle(r);
                Shadow(g, p, s, a);
                using (var b = new SolidBrush(Al(245, Color.White, a))) g.FillPath(b, p);
            }
            using (var outer = new Pen(Al(255, Theme.Red, a), s * 0.05f)) g.DrawRectangle(outer, r.X, r.Y, r.Width, r.Height);
            using (var inner = new Pen(Al(255, Theme.Red, a), s * 0.02f)) g.DrawRectangle(inner, r.X + s * 0.08f, r.Y + s * 0.08f, r.Width - s * 0.16f, r.Height - s * 0.16f);
            using (var f = Fonts.Get("uiBold", s * 0.36f))
            using (var b = new SolidBrush(Al(255, Theme.Red, a)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.DrawString("TỐT LẮM!", f, b, new PointF(0, s * 0.02f), sf);
            }
        }

        static void Sao(Graphics g, float s, float a)
        {
            var pts = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double ang = -Math.PI / 2 + i * Math.PI / 5;
                float r = i % 2 == 0 ? s * 0.5f : s * 0.21f;
                pts[i] = new PointF(r * (float)Math.Cos(ang), r * (float)Math.Sin(ang) + s * 0.03f);
            }
            using (var p = new GraphicsPath())
            {
                p.AddPolygon(pts);
                Shadow(g, p, s, a);
                using (var halo = new Pen(Al(235, Color.White, a), s * 0.08f) { LineJoin = LineJoin.Round }) g.DrawPath(halo, p);
                using (var b = new SolidBrush(Al(255, Theme.Yellow, a))) g.FillPath(b, p);
                using (var e = new Pen(Al(255, Color.FromArgb(184, 138, 0), a), s * 0.03f) { LineJoin = LineJoin.Round }) g.DrawPath(e, p);
            }
        }
    }
}
