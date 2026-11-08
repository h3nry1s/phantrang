using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PhanTrang
{
    /// <summary>A pre-rendered pointer (chalk, ruler or pen) and its soft shadow. Tip = pixel position of the point.</summary>
    public class Sprite
    {
        public Bitmap Img, Shadow;
        public PointF Tip;
    }

    /// <summary>Draws the pointers. Each is drawn along +x with its point at the origin, then tilted 40° so the
    /// body trails down to the right, the way a teacher holds chalk or a ruler against the board.</summary>
    public static class Pointers
    {
        public const float Angle = 40f;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly List<string> order = new List<string>();

        /// <summary>len = length of the chalk in pixels; the ruler and pen scale from it.</summary>
        public static Sprite Get(string kind, int len)
        {
            len = Math.Max(16, len);
            string key = kind + "|" + len;
            Sprite s;
            if (cache.TryGetValue(key, out s)) return s;
            s = Build(kind, len);
            cache[key] = s;
            order.Add(key);
            while (order.Count > 6)
            {
                Sprite old;
                if (cache.TryGetValue(order[0], out old)) { old.Img.Dispose(); old.Shadow.Dispose(); cache.Remove(order[0]); }
                order.RemoveAt(0);
            }
            return s;
        }

        static void Dims(string kind, float len, out float L, out float T)
        {
            switch (kind)
            {
                case "ruler": L = len * 3.6f; T = len * 0.2f; break;
                case "pen": L = len * 1.25f; T = len * 0.2f; break;
                default: L = len; T = len * 0.3f; break;
            }
        }

        /// <summary>The box (relative to the point) that an L x T pointer covers once tilted by Angle.</summary>
        static RectangleF TiltedBox(float L, float T)
        {
            using (var m = new Matrix())
            {
                m.Rotate(Angle);
                var corners = new[] { new PointF(0, -T / 2), new PointF(L, -T / 2), new PointF(L, T / 2), new PointF(0, T / 2) };
                m.TransformPoints(corners);
                return Geo.Box(corners, 0);
            }
        }

        static Sprite Build(string kind, int len)
        {
            float L, T;
            Dims(kind, len, out L, out T);
            float soft = Math.Max(3f, len * 0.07f);
            float pad = soft * 2.5f + 4;
            var box = TiltedBox(L, T);
            int w = (int)Math.Ceiling(box.Width + 2 * pad), h = (int)Math.Ceiling(box.Height + 2 * pad);
            var tip = new PointF(pad - box.Left, pad - box.Top);
            var img = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(img))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TranslateTransform(tip.X, tip.Y);
                g.RotateTransform(Angle);
                Draw(g, kind, L, T);
            }
            return new Sprite { Img = img, Shadow = SoftShadow(kind, L, T, w, h, tip, soft), Tip = tip };
        }

        /// <summary>
        /// The pointer's silhouette, softened: drawn small (one pixel per 'soft' pixels) and scaled back up with
        /// bicubic filtering, which spreads every edge into a smooth gradient. Then faded to a light shadow.
        /// </summary>
        static Bitmap SoftShadow(string kind, float L, float T, int w, int h, PointF tip, float soft)
        {
            float k = 1f / Math.Max(1.5f, soft * 0.75f);
            int sw = Math.Max(2, (int)Math.Ceiling(w * k)), sh = Math.Max(2, (int)Math.Ceiling(h * k));
            using (var small = new Bitmap(sw, sh, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(small))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.ScaleTransform(k, k);
                    g.TranslateTransform(tip.X, tip.Y);
                    g.RotateTransform(Angle);
                    using (var path = Outline(kind, L, T))
                    using (var b = new SolidBrush(Color.FromArgb(87, 0, 0, 0))) g.FillPath(b, path);   // 87 ≈ a third of full black
                }
                var shadow = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(shadow))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(small, new Rectangle(0, 0, w, h), 0, 0, sw, sh, GraphicsUnit.Pixel);
                }
                return shadow;
            }
        }

        static GraphicsPath Outline(string kind, float L, float T)
        {
            var p = new GraphicsPath();
            float h = T / 2;
            switch (kind)
            {
                case "ruler":
                    p.AddPolygon(new[] { new PointF(0, 0), new PointF(T * 0.6f, -h), new PointF(L, -h), new PointF(L, h), new PointF(T * 0.6f, h) });
                    break;
                case "pen":
                    p.AddPolygon(new[] { new PointF(0, 0), new PointF(L * 0.15f, -h * 0.8f), new PointF(L * 0.15f, -h), new PointF(L - h, -h), new PointF(L, 0), new PointF(L - h, h), new PointF(L * 0.15f, h), new PointF(L * 0.15f, h * 0.8f) });
                    break;
                default:
                    // a stick of chalk, its writing end worn to a blunt point
                    p.AddLine(L * 0.02f, -h * 0.25f, L * 0.17f, -h);
                    p.AddLine(L * 0.17f, -h, L - h * 0.6f, -h);
                    p.AddArc(L - h * 1.2f, -h, h * 1.2f, T, 270, 180);
                    p.AddLine(L - h * 0.6f, h, L * 0.17f, h);
                    p.AddLine(L * 0.17f, h, L * 0.02f, h * 0.25f);
                    p.AddArc(-L * 0.01f, -h * 0.25f, h * 0.5f, h * 0.5f, 90, 180);
                    p.CloseFigure();
                    break;
            }
            return p;
        }

        static void Draw(Graphics g, string kind, float L, float T)
        {
            float h = T / 2;
            using (var path = Outline(kind, L, T))
            {
                switch (kind)
                {
                    case "ruler":
                        using (var b = new LinearGradientBrush(new RectangleF(0, -h, L, T), Color.FromArgb(239, 201, 133), Color.FromArgb(199, 148, 72), 90f))
                            g.FillPath(b, path);
                        g.SetClip(path);
                        using (var hi = new SolidBrush(Color.FromArgb(90, 255, 255, 255))) g.FillRectangle(hi, 0, -h, L, T * 0.22f);
                        using (var tick = new Pen(Theme.WoodDark, Math.Max(1f, T * 0.06f)))
                        {
                            float step = L / 40f;
                            int i = 0;
                            for (float x = T * 0.9f; x < L - step * 0.5f; x += step, i++)
                                g.DrawLine(tick, x, h, x, h - T * (i % 5 == 0 ? 0.55f : 0.32f));
                        }
                        g.ResetClip();
                        using (var edge = new Pen(Color.FromArgb(168, 122, 58), 1f)) g.DrawPath(edge, path);
                        break;
                    case "pen":
                        using (var nib = new SolidBrush(Color.FromArgb(217, 207, 180))) g.FillPath(nib, path);
                        g.SetClip(path);
                        using (var grip = new SolidBrush(Color.FromArgb(63, 42, 115))) g.FillRectangle(grip, L * 0.15f, -h, L * 0.15f, T);
                        using (var body = new SolidBrush(Theme.Purple)) g.FillRectangle(body, L * 0.3f, -h, L * 0.56f, T);
                        using (var cap = new SolidBrush(Color.FromArgb(63, 42, 115))) g.FillRectangle(cap, L * 0.86f, -h, L * 0.14f, T);
                        using (var hi = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillRectangle(hi, L * 0.15f, -h, L * 0.85f, T * 0.25f);
                        g.ResetClip();
                        using (var clip = new Pen(Color.FromArgb(205, 196, 176), Math.Max(1.2f, T * 0.12f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                            g.DrawLine(clip, L * 0.68f, -h * 0.35f, L * 0.92f, -h * 0.35f);
                        using (var dot = new SolidBrush(Color.FromArgb(63, 42, 115))) g.FillEllipse(dot, -T * 0.06f, -T * 0.06f, T * 0.12f, T * 0.12f);
                        break;
                    default:
                        using (var b = new SolidBrush(Color.FromArgb(246, 243, 234))) g.FillPath(b, path);
                        g.SetClip(path);
                        using (var worn = new SolidBrush(Color.FromArgb(230, 223, 207))) g.FillRectangle(worn, -2, -h, L * 0.17f + 2, T);
                        using (var hi = new SolidBrush(Color.FromArgb(220, 255, 255, 255))) g.FillRectangle(hi, L * 0.17f, -h, L, T * 0.3f);
                        using (var dust = new SolidBrush(Color.FromArgb(60, 160, 150, 130)))
                        {
                            g.FillEllipse(dust, L * 0.42f, h * 0.2f, T * 0.09f, T * 0.09f);
                            g.FillEllipse(dust, L * 0.66f, -h * 0.05f, T * 0.07f, T * 0.07f);
                            g.FillEllipse(dust, L * 0.3f, h * 0.45f, T * 0.06f, T * 0.06f);
                        }
                        g.ResetClip();
                        using (var edge = new Pen(Color.FromArgb(150, 140, 118), Math.Max(1.2f, T * 0.05f))) g.DrawPath(edge, path);
                        break;
                }
            }
        }

        static readonly Color LaserRed = Color.FromArgb(229, 57, 53);

        /// <summary>
        /// The laser dot: a red core with a pale hot centre, inside a glow made of stacked translucent discs
        /// (each ring adds a little red, so the glow is strongest near the dot). Pressing makes the glow wider.
        /// </summary>
        public static void DrawLaser(Graphics g, float x, float y, float r, double press)
        {
            float reach = r * (3.2f + (float)press);
            const int rings = 7;
            using (var glow = new SolidBrush(Color.FromArgb(24, LaserRed)))
                for (int i = 0; i < rings; i++)
                {
                    float rr = r + (reach - r) * (rings - i) / rings;
                    g.FillEllipse(glow, x - rr, y - rr, 2 * rr, 2 * rr);
                }
            using (var core = new SolidBrush(LaserRed)) g.FillEllipse(core, x - r, y - r, 2 * r, 2 * r);
            float hot = r * 0.42f;
            using (var centre = new SolidBrush(Color.FromArgb(240, 255, 224, 218))) g.FillEllipse(centre, x - hot, y - hot, 2 * hot, 2 * hot);
        }

        /// <summary>A small picture of a pointer for tiles and menus (no shadow).</summary>
        public static void DrawIcon(Graphics g, string kind, RectangleF r)
        {
            if (kind == "laser")
            {
                float rr = Math.Min(r.Width, r.Height) * 0.17f;
                DrawLaser(g, r.X + r.Width / 2, r.Y + r.Height / 2, rr, 0);
                return;
            }
            float L, T;
            float len = 100;
            Dims(kind, len, out L, out T);
            if (kind == "ruler") L = len * 1.5f;   // a short piece of ruler reads better in a small tile
            var box = TiltedBox(L, T);
            float k = 0.9f * Math.Min(r.Width / box.Width, r.Height / box.Height);
            var st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(r.X + (r.Width - box.Width * k) / 2 - box.Left * k, r.Y + (r.Height - box.Height * k) / 2 - box.Top * k);
            g.ScaleTransform(k, k);
            g.RotateTransform(Angle);
            Draw(g, kind, L, T);
            g.Restore(st);
        }
    }
}
