using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;

namespace PhanTrang
{
    /// <summary>
    /// Everything drawn for the "Lớp học" tools: their icons, the countdown clock, the name picker,
    /// group cards, the team scoreboard and the geometry instruments. Board green, chalk, wood, cream paper.
    /// </summary>
    public static class ClassArt
    {
        // ---------------------------------------------------------------- palette

        static readonly Color ChalkYellow = Color.FromArgb(255, 214, 92);
        static readonly Color ChalkOrange = Color.FromArgb(255, 150, 66);
        static readonly Color ChalkRed = Color.FromArgb(255, 96, 84);
        static readonly Color ChalkPink = Color.FromArgb(255, 150, 190);
        static readonly Color ChalkBlue = Color.FromArgb(120, 200, 255);
        static readonly Color ChalkGreen = Color.FromArgb(150, 232, 150);
        static readonly Color Gold = Color.FromArgb(246, 190, 40);
        static readonly Color GoldDark = Color.FromArgb(178, 120, 10);
        static readonly Color[] Confetti = { ChalkYellow, ChalkPink, ChalkBlue, ChalkGreen, Color.White, ChalkOrange };

        static readonly Color[] Groups =
        {
            Color.FromArgb(214, 64, 52),   // đỏ
            Color.FromArgb(36, 110, 200),  // xanh dương
            Color.FromArgb(46, 150, 72),   // xanh lá
            Color.FromArgb(236, 140, 20),  // cam
            Color.FromArgb(124, 76, 184),  // tím
            Color.FromArgb(0, 150, 150),   // xanh ngọc
            Color.FromArgb(218, 70, 140),  // hồng
            Color.FromArgb(132, 96, 50),   // nâu gỗ
        };

        /// <summary>A distinct colour for group/team i (0-based); 8 colours, then repeats.</summary>
        public static Color GroupColor(int i)
        {
            int n = Groups.Length;
            return Groups[((i % n) + n) % n];
        }

        // ---------------------------------------------------------------- small helpers

        static Color A(Color c, int a) { return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c.R, c.G, c.B); }

        static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        static Pen RPen(Color c, float w)
        {
            return new Pen(c, Math.Max(0.5f, w)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        }

        static float Clamp(float v, float lo, float hi) { return v < lo ? lo : v > hi ? hi : v; }

        /// <summary>Deterministic pseudo-random 0..1 from two integers.</summary>
        static float Rnd(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 374761393u + (uint)b * 668265263u + 0x9E3779B9u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        static int Seed(string s)
        {
            if (string.IsNullOrEmpty(s)) return 7;
            unchecked { int h = 17; foreach (char c in s) h = h * 31 + c; return h; }
        }

        static double Frac(double v) { return v - Math.Floor(v); }

        static GraphicsPath Round(RectangleF r, float radius) { return Draw.Round(r, Math.Max(0.5f, radius)); }

        static RectangleF Square(RectangleF r, float scale)
        {
            float s = Math.Min(r.Width, r.Height) * scale;
            return new RectangleF(r.X + (r.Width - s) / 2, r.Y + (r.Height - s) / 2, s, s);
        }

        /// <summary>Text as a path at px pixels; bounds out. Caller disposes.</summary>
        static GraphicsPath TextPath(string text, string fontId, float px, out RectangleF bounds)
        {
            var p = new GraphicsPath();
            using (var f = Fonts.Get(fontId, Math.Max(1, px)))
            using (var sf = new StringFormat(StringFormat.GenericTypographic))
            {
                sf.FormatFlags |= StringFormatFlags.NoWrap;
                p.AddString(text ?? "", f.FontFamily, (int)f.Style, f.Size, PointF.Empty, sf);
            }
            bounds = p.GetBounds();
            return p;
        }

        /// <summary>The largest px (≤ maxPx) at which text is at most maxW wide.</summary>
        static float FitPx(string text, string fontId, float maxPx, float maxW)
        {
            if (string.IsNullOrEmpty(text) || maxW <= 1) return Math.Max(1, maxPx);
            RectangleF b;
            using (TextPath(text, fontId, 100, out b)) { }
            if (b.Width <= 0) return maxPx;
            return Math.Max(1, Math.Min(maxPx, 100 * maxW / b.Width));
        }

        /// <summary>Draws text centred on c (by its ink bounds), optional outline and soft shadow.</summary>
        static void CenterText(Graphics g, string text, string fontId, float px, PointF c, Color fill, Color outline, float outlineW, float shadow)
        {
            if (string.IsNullOrEmpty(text) || px < 1) return;
            RectangleF b;
            using (var p = TextPath(text, fontId, px, out b))
            {
                using (var m = new Matrix()) { m.Translate(c.X - b.X - b.Width / 2, c.Y - b.Y - b.Height / 2); p.Transform(m); }
                if (shadow > 0)
                {
                    var st = g.Save();
                    g.TranslateTransform(shadow * 0.5f, shadow);
                    using (var sb = new SolidBrush(Color.FromArgb(Math.Min(90, (int)(fill.A * 0.35f)), 0, 0, 0))) g.FillPath(sb, p);
                    g.Restore(st);
                }
                if (outlineW > 0 && outline.A > 0) using (var op = RPen(outline, outlineW)) g.DrawPath(op, p);
                using (var fb = new SolidBrush(fill)) g.FillPath(fb, p);
            }
        }

        /// <summary>Text with its baseline box starting at a point, left/centre/right aligned, using GDI+ strings (small labels).</summary>
        static void Label(Graphics g, string text, string fontId, float px, Color c, RectangleF box, StringAlignment h, StringAlignment v)
        {
            if (string.IsNullOrEmpty(text) || px < 1) return;
            using (var f = Fonts.Get(fontId, px))
            using (var b = new SolidBrush(c))
            using (var sf = new StringFormat { Alignment = h, LineAlignment = v, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(text, f, b, box, sf);
        }

        static GraphicsPath Star(PointF c, float r, float inner, int points, float rot)
        {
            var pts = new PointF[points * 2];
            for (int i = 0; i < pts.Length; i++)
            {
                double a = rot * Math.PI / 180 - Math.PI / 2 + i * Math.PI / points;
                float rr = i % 2 == 0 ? r : r * inner;
                pts[i] = new PointF(c.X + (float)Math.Cos(a) * rr, c.Y + (float)Math.Sin(a) * rr);
            }
            var p = new GraphicsPath();
            p.AddPolygon(pts);
            return p;
        }

        static void Sparkle(Graphics g, PointF c, float r, Color col)
        {
            if (r < 0.5f) return;
            using (var p = Star(c, r, 0.28f, 4, 0))
            using (var b = new SolidBrush(col)) g.FillPath(b, p);
        }

        /// <summary>One chalk-coloured confetti piece: a little rounded stick, rotated.</summary>
        static void Piece(Graphics g, PointF c, float len, float angle, Color col)
        {
            if (len < 0.5f) return;
            var st = g.Save();
            g.TranslateTransform(c.X, c.Y);
            g.RotateTransform(angle);
            using (var p = Round(new RectangleF(-len / 2, -len * 0.2f, len, len * 0.4f), len * 0.2f))
            using (var b = new SolidBrush(col)) g.FillPath(b, p);
            g.Restore(st);
        }

        /// <summary>Confetti drifting down through area (clipped to it), looping with now.</summary>
        static void FallingConfetti(Graphics g, RectangleF area, int seed, int count, float size, double now, int alpha)
        {
            var st = g.Save();
            g.SetClip(area, CombineMode.Intersect);
            for (int i = 0; i < count; i++)
            {
                float rx = Rnd(seed, i * 3), rs = Rnd(seed, i * 3 + 1), rc = Rnd(seed, i * 3 + 2);
                double speed = 0.18 + 0.22 * rs;
                double t = Frac(rc + now * speed);
                float x = area.X + area.Width * rx + (float)Math.Sin(now * 2 + i) * size * 0.8f;
                float y = area.Y - size * 2 + (float)t * (area.Height + size * 4);
                Piece(g, new PointF(x, y), size * (0.7f + 0.6f * rs), (float)(now * 160 * (rs - 0.5) + rx * 360), A(Confetti[i % Confetti.Length], alpha));
            }
            g.Restore(st);
        }

        /// <summary>A soft, edgeless chalk smudge, like a board that has been wiped.</summary>
        static void Smudge(Graphics g, RectangleF e)
        {
            if (e.Width < 2 || e.Height < 2) return;
            using (var p = new GraphicsPath())
            {
                p.AddEllipse(e);
                using (var pg = new PathGradientBrush(p) { CenterColor = Color.FromArgb(16, 255, 255, 255), SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) } })
                    g.FillPath(pg, p);
            }
        }

        static void SoftShadow(Graphics g, GraphicsPath p, float dx, float dy, int alpha)
        {
            var st = g.Save();
            g.TranslateTransform(dx, dy);
            using (var b = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0))) g.FillPath(b, p);
            g.TranslateTransform(-dx * 0.45f, -dy * 0.45f);
            using (var b = new SolidBrush(Color.FromArgb(alpha / 2, 0, 0, 0))) g.FillPath(b, p);
            g.Restore(st);
        }

        static void Begin(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        // ================================================================ ICONS

        /// <summary>Line icon on the 24-unit grid of ToolIcons, with one cheerful accent colour when !onDark.</summary>
        public static bool Icon(Graphics g, string kind, Rectangle r, Color ink, bool onDark)
        {
            switch (kind)
            {
                case "class": case "timer": case "picker": case "groups": case "contest":
                case "ruler": case "protractor": case "setsquare": case "compass": break;
                default: return false;
            }
            float u = r.Width / 24f;
            var st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            Func<float, float, PointF> P = (x, y) => new PointF((float)Math.Round(r.Left + x * u), (float)Math.Round(r.Top + y * u));
            Func<float, float, float, float, RectangleF> R = (x, y, w, h) => new RectangleF(r.Left + x * u, r.Top + y * u, w * u, h * u);
            float pw = Math.Max(2f, (float)Math.Round(1.9f * u));
            Func<Color, int, Color> accent = (c, a) => onDark ? Color.FromArgb(a * 2 / 5, ink) : Color.FromArgb(a, c);
            using (var pen = RPen(ink, pw))
            using (var brush = new SolidBrush(ink))
            {
                switch (kind)
                {
                    case "class":
                        {
                            // trống trường: the red school drum with its stick — the sound of every Vietnamese school day
                            using (var body = new GraphicsPath())
                            {
                                body.AddLine(P(4, 8.5f), P(4, 16));
                                body.AddArc(R(4, 13, 16, 6), 180, -180);
                                body.AddLine(P(20, 16), P(20, 8.5f));
                                body.AddArc(R(4, 5.5f, 16, 6), 0, 180);
                                body.CloseFigure();
                                using (var b = new SolidBrush(accent(Color.FromArgb(214, 52, 40), 255))) g.FillPath(b, body);
                                g.DrawPath(pen, body);
                            }
                            using (var b = new SolidBrush(onDark ? A(ink, 40) : Color.FromArgb(252, 236, 196))) g.FillEllipse(b, R(4, 5.5f, 16, 6));
                            g.DrawEllipse(pen, R(4, 5.5f, 16, 6));
                            if (!onDark) using (var b = new SolidBrush(Theme.Yellow)) g.FillEllipse(b, R(10.4f, 12.4f, 3.2f, 3.2f));
                            else g.FillEllipse(brush, R(10.8f, 12.8f, 2.4f, 2.4f));
                            g.DrawLine(pen, P(13.5f, 8), P(21, 2.5f));
                            g.FillEllipse(brush, R(12.2f, 6.8f, 3, 3));
                            break;
                        }
                    case "timer":
                        {
                            var face = R(4, 5.5f, 16, 16);
                            using (var b = new SolidBrush(accent(Color.FromArgb(255, 112, 67), 255))) g.FillPie(b, face.X, face.Y, face.Width, face.Height, -90, 125);
                            g.DrawEllipse(pen, face);
                            g.DrawLine(pen, P(12, 2.5f), P(12, 5.5f));
                            g.DrawLine(pen, P(9.5f, 2.5f), P(14.5f, 2.5f));
                            g.DrawLine(pen, P(18.5f, 5.5f), P(20.3f, 3.7f));
                            g.DrawLine(pen, P(12, 13.5f), P(12, 8.5f));
                            g.FillEllipse(brush, R(10.7f, 12.2f, 2.6f, 2.6f));
                            break;
                        }
                    case "picker":
                        {
                            // vòng quay may mắn: a lucky wheel with a pointer
                            var w = R(3, 4.5f, 18, 18);
                            Color[] seg = { Color.FromArgb(229, 57, 53), Theme.Yellow, Color.FromArgb(46, 150, 72), Color.FromArgb(36, 120, 210), Color.FromArgb(255, 140, 30), Color.FromArgb(150, 90, 200) };
                            for (int i = 0; i < 6; i++)
                            {
                                Color c = onDark ? (i % 2 == 0 ? A(ink, 90) : Color.Transparent) : seg[i];
                                if (c.A == 0) continue;
                                using (var b = new SolidBrush(c)) g.FillPie(b, w.X, w.Y, w.Width, w.Height, -90 + i * 60, 60);
                            }
                            g.DrawEllipse(pen, w);
                            float cx = w.X + w.Width / 2, cy = w.Y + w.Height / 2;
                            using (var thin = RPen(ink, Math.Max(1f, pw * 0.55f)))
                                for (int i = 0; i < 6; i++)
                                {
                                    double a = (-90 + i * 60) * Math.PI / 180;
                                    g.DrawLine(thin, cx, cy, cx + (float)Math.Cos(a) * w.Width / 2, cy + (float)Math.Sin(a) * w.Width / 2);
                                }
                            using (var hub = new SolidBrush(onDark ? ink : Color.White)) g.FillEllipse(hub, R(10, 11.5f, 4, 4));
                            g.DrawEllipse(pen, R(10, 11.5f, 4, 4));
                            var tri = new[] { P(9, 1.5f), P(15, 1.5f), P(12, 6.5f) };
                            g.FillPolygon(brush, tri);
                            using (var tp = RPen(ink, pw * 0.6f)) g.DrawPolygon(tp, tri);
                            break;
                        }
                    case "groups":
                        {
                            // three classmates, each in their group colour
                            Action<float, float, float, Color> person = (x, y, s, c) =>
                            {
                                // a round head over a bell-shaped body; side by side, no overlaps, so it stays clear at 20 px
                                var head = R(x - 2.5f * s, y - 2.5f * s, 5 * s, 5 * s);
                                using (var body = new GraphicsPath())
                                {
                                    body.AddArc(R(x - 3.4f * s, y + 3.6f * s, 6.8f * s, 14 * s), 180, 180);
                                    body.CloseFigure();
                                    using (var b = new SolidBrush(onDark ? A(ink, 70) : c)) g.FillPath(b, body);
                                    g.DrawPath(pen, body);
                                }
                                using (var b = new SolidBrush(onDark ? Color.Transparent : c)) g.FillEllipse(b, head);
                                g.DrawEllipse(pen, head);
                            };
                            person(4.2f, 9.5f, 0.9f, GroupColor(1));
                            person(19.8f, 9.5f, 0.9f, GroupColor(2));
                            person(12, 6.5f, 1f, GroupColor(0));
                            g.DrawLine(pen, P(2.5f, 21), P(21.5f, 21));
                            break;
                        }
                    case "contest":
                        {
                            // a trophy cup with a star
                            using (var cup = new GraphicsPath())
                            {
                                cup.AddLine(P(6.5f, 3.5f), P(17.5f, 3.5f));
                                cup.AddBezier(P(17.5f, 3.5f), P(17.5f, 10), P(15.5f, 13.5f), P(12, 13.5f));
                                cup.AddBezier(P(12, 13.5f), P(8.5f, 13.5f), P(6.5f, 10), P(6.5f, 3.5f));
                                cup.CloseFigure();
                                using (var b = new SolidBrush(accent(Gold, 255))) g.FillPath(b, cup);
                                g.DrawPath(pen, cup);
                            }
                            g.DrawBezier(pen, P(6.8f, 5.5f), P(2.5f, 5), P(2.8f, 10.5f), P(7.8f, 10.5f));
                            g.DrawBezier(pen, P(17.2f, 5.5f), P(21.5f, 5), P(21.2f, 10.5f), P(16.2f, 10.5f));
                            g.DrawLine(pen, P(12, 13.5f), P(12, 17.5f));
                            g.DrawLine(pen, P(8, 20.5f), P(16, 20.5f));
                            g.DrawLine(pen, P(9.5f, 17.5f), P(14.5f, 17.5f));
                            using (var s = Star(new PointF(r.Left + 12 * u, r.Top + 7.6f * u), 3 * u, 0.45f, 5, 0))
                            using (var b = new SolidBrush(onDark ? ink : Color.White)) g.FillPath(b, s);
                            break;
                        }
                    case "ruler":
                        {
                            var body = new[] { P(2, 16.5f), P(16.5f, 2), P(22, 7.5f), P(7.5f, 22) };
                            using (var b = new SolidBrush(accent(Theme.Wood, 255))) g.FillPolygon(b, body);
                            g.DrawPolygon(pen, body);
                            using (var tp = RPen(ink, Math.Max(1.5f, pw * 0.75f)))
                                for (int i = 1; i <= 5; i++)
                                {
                                    float t = i / 6f, x = 2 + 14.5f * t, y = 16.5f - 14.5f * t, len = i % 2 == 1 ? 3.6f : 2.2f;
                                    g.DrawLine(tp, P(x, y), P(x + len * 0.707f, y + len * 0.707f));
                                }
                            break;
                        }
                    case "protractor":
                        {
                            var disc = R(2.5f, 7.5f, 19, 19);
                            using (var b = new SolidBrush(accent(Color.FromArgb(150, 205, 245), 255))) g.FillPie(b, disc.X, disc.Y, disc.Width, disc.Height, 180, 180);
                            using (var hole = new SolidBrush(onDark ? Color.Transparent : Color.FromArgb(225, 242, 252)))
                                if (!onDark) g.FillPie(hole, r.Left + 8 * u, r.Top + 13 * u, 8 * u, 8 * u, 180, 180);
                            g.DrawArc(pen, disc, 180, 180);
                            g.DrawLine(pen, P(2.5f, 17), P(21.5f, 17));
                            g.DrawArc(pen, R(8, 13, 8, 8), 180, 180);
                            using (var tp = RPen(ink, Math.Max(1.5f, pw * 0.7f)))
                                for (int a = 30; a <= 150; a += 30)
                                {
                                    double t = a * Math.PI / 180;
                                    float c = (float)Math.Cos(t), s = (float)Math.Sin(t);
                                    g.DrawLine(tp, P(12 + c * 9.5f, 17 - s * 9.5f), P(12 + c * 7, 17 - s * 7));
                                }
                            g.FillEllipse(brush, R(11, 16, 2, 2));
                            break;
                        }
                    case "setsquare":
                        {
                            using (var tri = new GraphicsPath())
                            {
                                tri.AddPolygon(new[] { P(3.5f, 2.5f), P(3.5f, 20.5f), P(21.5f, 20.5f) });
                                tri.AddPolygon(new[] { P(7.5f, 11.5f), P(7.5f, 16.5f), P(12.5f, 16.5f) });
                                using (var b = new SolidBrush(accent(Color.FromArgb(250, 200, 70), 255))) g.FillPath(b, tri);
                                g.DrawPath(pen, tri);
                            }
                            using (var tp = RPen(ink, Math.Max(1.5f, pw * 0.7f)))
                                for (int i = 1; i <= 3; i++) g.DrawLine(tp, P(3.5f + i * 4.5f, 20.5f), P(3.5f + i * 4.5f, i == 2 ? 18 : 18.8f));
                            break;
                        }
                    case "compass":
                        {
                            // a chalk arc being drawn by the compass
                            using (var arc = RPen(onDark ? A(ink, 150) : Color.FromArgb(255, 112, 67), pw))
                                g.DrawArc(arc, r.Left + (6 - 13) * u, r.Top + (20.5f - 13) * u, 26 * u, 26 * u, -38, 32);
                            g.DrawLine(pen, P(12, 6), P(6, 20.5f));
                            g.DrawLine(pen, P(12, 6), P(17, 17));
                            using (var lead = RPen(onDark ? ink : Color.FromArgb(214, 52, 40), pw * 1.25f)) g.DrawLine(lead, P(17, 17), P(18.4f, 20.6f));
                            g.DrawLine(pen, P(12, 1.5f), P(12, 4));
                            using (var b = new SolidBrush(accent(Gold, 255))) g.FillEllipse(b, R(9.6f, 3.6f, 4.8f, 4.8f));
                            g.DrawEllipse(pen, R(9.6f, 3.6f, 4.8f, 4.8f));
                            g.FillEllipse(brush, R(5, 19.5f, 2, 2));
                            break;
                        }
                }
            }
            g.Restore(st);
            return true;
        }

        // ================================================================ COUNTDOWN

        static string Clock(double remaining)
        {
            int secs = (int)Math.Ceiling(Math.Max(0, remaining) - 1e-6);
            int h = secs / 3600, m = (secs / 60) % 60, s = secs % 60;
            if (h > 0) return string.Format("{0}:{1:00}:{2:00}", h, m, s);
            return string.Format("{0:00}:{1:00}", m, s);
        }

        /// <summary>The countdown: a round chalkboard in a wooden rim, a chalk progress ring and big digits.</summary>
        public static void PaintTimer(Graphics g, RectangleF r, double remaining, double total, bool running, bool ended, double now)
        {
            if (r.Width < 4 || r.Height < 4) return;
            var st = g.Save();
            Begin(g);
            if (total <= 0) total = Math.Max(1, remaining);
            remaining = Math.Max(0, Math.Min(total, remaining));
            float f = (float)(remaining / total);
            bool urgent = !ended && remaining <= 10.0 && remaining > 0;

            var sq = Square(r, 1f);
            float S = sq.Width, R0 = S / 2 * 0.94f;
            var c = new PointF(sq.X + S / 2, sq.Y + S / 2 - S * 0.01f);
            string digits = Clock(remaining);

            // tiny: just a disc and the digits
            if (S < 90)
            {
                using (var b = new SolidBrush(ended ? Theme.Red : Theme.Board)) g.FillEllipse(b, c.X - R0, c.Y - R0, 2 * R0, 2 * R0);
                using (var arc = RPen(urgent ? ChalkRed : ChalkYellow, Math.Max(2, S * 0.06f)))
                    if (f > 0) g.DrawArc(arc, c.X - R0 * 0.85f, c.Y - R0 * 0.85f, R0 * 1.7f, R0 * 1.7f, -90, 360 * f);
                CenterText(g, digits, "uiBold", FitPx(digits.Length > 5 ? "0:00:00" : "00:00", "uiBold", R0 * 0.6f, R0 * 1.3f), c, Theme.Chalk, Color.Empty, 0, 0);
                g.Restore(st);
                return;
            }

            // shadow + wooden rim
            using (var rim = new GraphicsPath())
            {
                rim.AddEllipse(c.X - R0, c.Y - R0, 2 * R0, 2 * R0);
                SoftShadow(g, rim, 0, S * 0.022f, 50);
                using (var wood = new LinearGradientBrush(new PointF(c.X, c.Y - R0), new PointF(c.X, c.Y + R0), Color.FromArgb(232, 186, 112), Theme.WoodDark))
                    g.FillPath(wood, rim);
            }
            using (var grain = new Pen(Color.FromArgb(40, 90, 55, 15), Math.Max(1, S * 0.004f)))
                for (int i = 0; i < 5; i++)
                {
                    float rr = R0 * (0.925f + i * 0.016f);
                    g.DrawArc(grain, c.X - rr, c.Y - rr, 2 * rr, 2 * rr, 200 + i * 37, 70 + i * 11);
                }
            float Rb = R0 * 0.885f;
            var boardRect = new RectangleF(c.X - Rb, c.Y - Rb, 2 * Rb, 2 * Rb);
            using (var bp = new GraphicsPath())
            {
                bp.AddEllipse(boardRect);
                using (var pg = new PathGradientBrush(bp))
                {
                    pg.CenterPoint = new PointF(c.X - Rb * 0.25f, c.Y - Rb * 0.35f);
                    pg.CenterColor = Color.FromArgb(48, 104, 80);
                    pg.SurroundColors = new[] { Color.FromArgb(26, 64, 50) };
                    g.FillPath(pg, bp);
                }
                // faint chalk smudges, like a real board that has been wiped
                var cs = g.Save();
                g.SetClip(bp, CombineMode.Intersect);
                for (int i = 0; i < 6; i++)
                    Smudge(g, new RectangleF(c.X + (Rnd(3, i) - 0.7f) * Rb * 1.4f, c.Y + (Rnd(5, i) - 0.6f) * Rb * 1.4f, Rb * (0.5f + Rnd(7, i)), Rb * (0.18f + 0.2f * Rnd(9, i))));
                if (ended)
                {
                    using (var hot = new PathGradientBrush(bp) { CenterColor = Color.FromArgb(0, 230, 60, 50), SurroundColors = new[] { Color.FromArgb((int)(55 + 40 * Math.Sin(now * 7)), 230, 60, 50) } })
                        g.FillPath(hot, bp);
                }
                g.Restore(cs);
            }
            using (var inner = new Pen(Color.FromArgb(90, 40, 22, 5), Math.Max(1, S * 0.008f))) g.DrawEllipse(inner, boardRect);

            // minute-like ticks around the board
            float tickW = Math.Max(1, S * 0.006f);
            using (var tick = RPen(Color.FromArgb(70, Theme.Chalk), tickW))
            using (var tickBig = RPen(Color.FromArgb(140, Theme.Chalk), tickW * 1.8f))
                for (int i = 0; i < 60; i++)
                {
                    double a = i * Math.PI / 30;
                    float co = (float)Math.Cos(a), si = (float)Math.Sin(a), r1 = Rb * 0.94f, r2 = Rb * (i % 5 == 0 ? 0.87f : 0.9f);
                    g.DrawLine(i % 5 == 0 ? tickBig : tick, c.X + co * r1, c.Y + si * r1, c.X + co * r2, c.Y + si * r2);
                }

            // progress ring
            Color ring = ended ? ChalkRed : urgent ? ChalkRed : f <= 0.25f ? ChalkOrange : ChalkYellow;
            float Rp = Rb * 0.77f, rw = Math.Max(3, S * 0.05f);
            var pr = new RectangleF(c.X - Rp, c.Y - Rp, 2 * Rp, 2 * Rp);
            using (var track = new Pen(Color.FromArgb(38, 255, 255, 255), rw)) g.DrawEllipse(track, pr);
            if (ended)
                using (var arc = new Pen(A(ChalkRed, (int)(170 + 80 * Math.Sin(now * 7))), rw)) g.DrawEllipse(arc, pr);
            if (!ended && f > 0.0005f)
            {
                float sweep = 360 * f;
                using (var glow = RPen(A(ring, urgent ? 70 : 40), rw * 1.9f)) g.DrawArc(glow, pr, -90, sweep);
                using (var arc = RPen(ring, rw)) g.DrawArc(arc, pr, -90, sweep);
                // chalk texture: tiny dark specks along the stroke
                using (var speck = new SolidBrush(Color.FromArgb(90, 255, 255, 255)))
                    for (int i = 0; i < 60; i++)
                    {
                        float t = Rnd(11, i);
                        if (t > f) continue;
                        double a = (-90 + 360 * t) * Math.PI / 180;
                        float off = (Rnd(13, i) - 0.5f) * rw * 0.7f, sz = rw * (0.04f + 0.06f * Rnd(17, i));
                        g.FillEllipse(speck, c.X + (float)Math.Cos(a) * (Rp + off) - sz, c.Y + (float)Math.Sin(a) * (Rp + off) - sz, 2 * sz, 2 * sz);
                    }
                // the head of the ring: a stub of chalk
                double ha = (-90 + sweep) * Math.PI / 180;
                var head = new PointF(c.X + (float)Math.Cos(ha) * Rp, c.Y + (float)Math.Sin(ha) * Rp);
                using (var hb = new SolidBrush(Color.White)) g.FillEllipse(hb, head.X - rw * 0.62f, head.Y - rw * 0.62f, rw * 1.24f, rw * 1.24f);
                using (var hp = new Pen(ring, Math.Max(1, rw * 0.22f))) g.DrawEllipse(hp, head.X - rw * 0.62f, head.Y - rw * 0.62f, rw * 1.24f, rw * 1.24f);
            }

            if (ended)
            {
                // "Hết giờ!" wobbling, with confetti bursting from the middle
                var cs = g.Save();
                g.SetClip(r, CombineMode.Intersect);
                for (int i = 0; i < 46; i++)
                {
                    double t = Frac(now * 0.55 + Rnd(21, i));
                    double ang = Rnd(23, i) * Math.PI * 2;
                    float dist = (float)(Rb * 0.25 + t * R0 * 1.05);
                    var p = new PointF(c.X + (float)Math.Cos(ang) * dist, c.Y + (float)Math.Sin(ang) * dist + (float)(t * t) * R0 * 0.3f);
                    int alpha = (int)(255 * Math.Min(1, (1 - t) * 2.2));
                    Piece(g, p, S * (0.022f + 0.018f * Rnd(25, i)), (float)(ang * 57 + now * 300 * (Rnd(27, i) - 0.5)), A(Confetti[i % Confetti.Length], alpha));
                }
                g.Restore(cs);
                for (int i = 0; i < 5; i++)
                {
                    double a = (i * 72 + 18) * Math.PI / 180;
                    float tw = (float)(0.5 + 0.5 * Math.Sin(now * 5 + i * 1.7));
                    Sparkle(g, new PointF(c.X + (float)Math.Cos(a) * Rb * 0.62f, c.Y + (float)Math.Sin(a) * Rb * 0.62f), Rb * (0.05f + 0.05f * tw), A(ChalkYellow, (int)(120 + 135 * tw)));
                }
                var ts = g.Save();
                float bounce = (float)Math.Abs(Math.Sin(now * 3.2)) * Rb * 0.04f;
                g.TranslateTransform(c.X, c.Y - Rb * 0.08f - bounce);
                g.RotateTransform((float)(Math.Sin(now * 6.4) * 5));
                float px = FitPx("Hết giờ!", "mali", Rb * 0.42f, Rb * 1.45f);
                CenterText(g, "Hết giờ!", "mali", px, PointF.Empty, ChalkYellow, Color.FromArgb(150, 60, 20, 10), px * 0.12f, px * 0.06f);
                g.Restore(ts);
                CenterText(g, digits, "uiBold", Rb * 0.16f, new PointF(c.X, c.Y + Rb * 0.38f), A(Theme.Chalk, 220), Color.Empty, 0, 0);
                g.Restore(st);
                return;
            }

            // big digits; in the last 10 seconds they thump once per second
            string template = digits.Length > 5 ? "0:00:00" : "00:00";
            float dpx = FitPx(template, "uiBold", Rb * 0.56f, Rp * 1.62f);
            float scale = 1;
            Color dcol = Theme.Chalk;
            if (urgent && running)
            {
                double phase = remaining - Math.Floor(remaining);
                scale = 1 + 0.16f * (float)Math.Pow(phase, 4);
                dcol = Mix(Color.FromArgb(255, 220, 210), Color.White, (float)(1 - phase));
            }
            else if (urgent) dcol = Color.FromArgb(255, 210, 200);
            RectangleF tb;
            using (TextPath(template, "uiBold", dpx, out tb)) { }
            var dc = new PointF(c.X, c.Y + Rb * 0.02f);
            var ds = g.Save();
            g.TranslateTransform(dc.X, dc.Y);
            g.ScaleTransform(scale, scale);
            if (urgent)
                using (var halo = new SolidBrush(Color.FromArgb(running ? (int)(70 * scale - 55) : 20, 255, 80, 60)))
                    g.FillEllipse(halo, -Rp * 0.8f, -Rp * 0.8f, Rp * 1.6f, Rp * 1.6f);
            // draw each glyph centred in a fixed cell so the digits do not jiggle as they change
            int colons = template.Count(ch0 => ch0 == ':');
            float colonW = dpx * 0.3f;
            float cell = (tb.Width - colons * colonW) / (template.Length - colons);
            float x = -tb.Width / 2;
            for (int i = 0; i < digits.Length; i++)
            {
                string ch = digits.Substring(i, 1);
                float w = ch == ":" ? colonW : cell;
                CenterText(g, ch, "uiBold", dpx, new PointF(x + w / 2, ch == ":" ? -dpx * 0.05f : 0), dcol, Color.Empty, 0, dpx * 0.05f);
                x += w;
            }
            g.Restore(ds);

            string caption = remaining >= total - 0.001 && !running ? "Sẵn sàng" : running ? "Đếm ngược" : "Tạm dừng";
            float lpx = Math.Max(8, Rb * 0.105f);
            CenterText(g, caption.ToUpperInvariant(), "uiSemi", lpx, new PointF(c.X, c.Y - Rb * 0.36f), A(Theme.Chalk, running ? 150 : 210), Color.Empty, 0, 0);
            if (!running && remaining < total - 0.001)
            {
                // pause bars under the digits
                float bw = Rb * 0.045f, bh = Rb * 0.13f, by = c.Y + Rb * 0.33f;
                using (var pb = new SolidBrush(A(ChalkYellow, 230)))
                {
                    using (var p1 = Round(new RectangleF(c.X - bw * 1.6f, by, bw, bh), bw / 2)) g.FillPath(pb, p1);
                    using (var p2 = Round(new RectangleF(c.X + bw * 0.6f, by, bw, bh), bw / 2)) g.FillPath(pb, p2);
                }
            }
            else
            {
                // total time, small, like chalk notes
                string tot = "/ " + Clock(total);
                CenterText(g, tot, "chalk", Math.Max(8, Rb * 0.13f), new PointF(c.X, c.Y + Rb * 0.4f), A(Theme.Chalk, 140), Color.Empty, 0, 0);
            }
            g.Restore(st);
        }

        // ================================================================ NAME PICKER

        /// <summary>The name picker: a framed chalkboard with a slot where names roll by, then "Mời em …" with confetti.</summary>
        public static void PaintNameCard(Graphics g, RectangleF r, string name, double spin, IList<string> reel, double now)
        {
            if (r.Width < 8 || r.Height < 8) return;
            var st = g.Save();
            Begin(g);
            spin = Math.Max(0, Math.Min(1, spin));
            float m = Math.Min(r.Width, r.Height);
            var card = new RectangleF(r.X + m * 0.02f, r.Y + m * 0.02f, r.Width - m * 0.04f, r.Height - m * 0.06f);
            float rad = m * 0.07f, frame = Math.Max(4, m * 0.055f);

            using (var outer = Round(card, rad))
            {
                SoftShadow(g, outer, 0, m * 0.025f, 50);
                using (var wood = new LinearGradientBrush(card, Color.FromArgb(232, 186, 112), Color.FromArgb(176, 122, 52), LinearGradientMode.Vertical)) g.FillPath(wood, outer);
                using (var edge = new Pen(Color.FromArgb(120, 90, 55, 15), Math.Max(1, m * 0.006f))) g.DrawPath(edge, outer);
            }
            var board = RectangleF.Inflate(card, -frame, -frame);
            using (var grain = new Pen(Color.FromArgb(36, 90, 55, 15), Math.Max(1, m * 0.004f)))
                for (int i = 0; i < 4; i++)
                {
                    float y = card.Y + frame * (0.25f + i * 0.17f);
                    g.DrawBezier(grain, card.X + rad, y, card.X + card.Width * 0.4f, y + frame * 0.12f, card.X + card.Width * 0.6f, y - frame * 0.1f, card.Right - rad, y);
                    float y2 = board.Bottom + frame * (0.25f + i * 0.17f);
                    g.DrawBezier(grain, card.X + rad, y2, card.X + card.Width * 0.3f, y2 - frame * 0.1f, card.X + card.Width * 0.7f, y2 + frame * 0.12f, card.Right - rad, y2);
                }
            var bpath = Round(board, rad * 0.5f);
            using (var bb = new LinearGradientBrush(board, Color.FromArgb(44, 98, 76), Color.FromArgb(28, 68, 53), 70f)) g.FillPath(bb, bpath);
            var cs0 = g.Save();
            g.SetClip(bpath, CombineMode.Intersect);
            for (int i = 0; i < 5; i++)
                Smudge(g, new RectangleF(board.X + Rnd(31, i) * board.Width * 0.7f, board.Y + Rnd(33, i) * board.Height * 0.8f, board.Width * (0.3f + 0.3f * Rnd(35, i)), board.Height * 0.22f));
            g.Restore(cs0);
            // chalk tray along the bottom with a stub of chalk
            float trayH = Math.Max(3, frame * 0.5f);
            using (var tray = new SolidBrush(Color.FromArgb(150, 100, 40)))
                g.FillRectangle(tray, board.X + board.Width * 0.08f, board.Bottom - trayH * 0.2f, board.Width * 0.84f, trayH);
            using (var chalk = Round(new RectangleF(board.X + board.Width * 0.7f, board.Bottom - trayH * 0.95f, frame * 1.8f, trayH * 0.8f), trayH * 0.4f))
            using (var cb = new SolidBrush(Color.FromArgb(248, 246, 238))) g.FillPath(cb, chalk);

            bool idle = name == null;
            bool stopped = !idle && spin >= 1;
            float bm = Math.Min(board.Width, board.Height);

            // top label
            string top = idle ? "Gọi tên ngẫu nhiên" : stopped ? "Mời em" : "Ai sẽ được gọi nhỉ…";
            float topPx = Math.Min(board.Height * 0.12f, FitPx(top, "chalk", board.Height * 0.12f, board.Width * 0.8f));
            CenterText(g, top, "chalk", topPx, new PointF(board.X + board.Width / 2, board.Y + board.Height * 0.16f), A(Theme.Chalk, 200), Color.Empty, 0, 0);

            // the slot
            var slot = new RectangleF(board.X + board.Width * 0.08f, board.Y + board.Height * 0.3f, board.Width * 0.84f, board.Height * 0.44f);
            float rowH = slot.Height * 0.62f;
            var mid = new PointF(slot.X + slot.Width / 2, slot.Y + slot.Height / 2);
            float namePxMax = Math.Min(rowH * 0.78f, board.Width * 0.2f);

            if (idle)
            {
                float bob = (float)Math.Sin(now * 2.6) * slot.Height * 0.05f;
                var qs = g.Save();
                g.TranslateTransform(mid.X, mid.Y + bob);
                g.RotateTransform((float)Math.Sin(now * 1.3) * 6);
                CenterText(g, "?", "mali", slot.Height * 0.9f, PointF.Empty, ChalkYellow, Color.Empty, 0, slot.Height * 0.04f);
                g.Restore(qs);
                for (int i = 0; i < 3; i++)
                {
                    float tw = (float)(0.5 + 0.5 * Math.Sin(now * 3 + i * 2.1));
                    Sparkle(g, new PointF(mid.X + slot.Height * (0.55f + 0.18f * i) * (i % 2 == 0 ? 1 : -1), mid.Y - slot.Height * (0.3f - 0.25f * i)), slot.Height * (0.06f + 0.05f * tw), A(Theme.Chalk, (int)(100 + 150 * tw)));
                }
                string hint = "Bấm Quay để gọi tên";
                CenterText(g, hint, "uiSemi", FitPx(hint, "uiSemi", board.Height * 0.075f, board.Width * 0.8f), new PointF(mid.X, board.Y + board.Height * 0.85f), A(Theme.Chalk, 170), Color.Empty, 0, 0);
            }
            else if (!stopped)
            {
                var items = new List<string>();
                if (reel != null) foreach (var s in reel) if (!string.IsNullOrEmpty(s)) items.Add(s);
                if (items.Count == 0 || items[items.Count - 1] != name) items.Add(name);   // the slot always stops on the called name
                int n = items.Count;
                double e = 1 - Math.Pow(1 - spin, 3);
                double pos = e * (n - 1);
                double speed = 3 * Math.Pow(1 - spin, 2) * (n - 1); // rows per unit spin, for blur
                using (var slotPath = Round(slot, slot.Height * 0.12f))
                {
                    using (var sb = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillPath(sb, slotPath);
                    var cs = g.Save();
                    g.SetClip(slotPath, CombineMode.Intersect);
                    int k = (int)Math.Floor(pos);
                    float blur = (float)Math.Min(rowH * 0.35, speed * rowH * 0.02);
                    for (int j = k - 1; j <= k + 2; j++)
                    {
                        if (j < 0 || j >= n) continue;
                        float y = mid.Y + (float)(j - pos) * rowH;
                        float d = Math.Abs(y - mid.Y) / rowH;
                        int alpha = (int)(255 * Math.Max(0, 1 - d * 0.75f));
                        float px = FitPx(items[j], "mali", namePxMax * 0.9f, slot.Width * 0.86f);
                        if (blur > 1)
                        {
                            CenterText(g, items[j], "mali", px, new PointF(mid.X, y - blur), A(Theme.Chalk, alpha / 4), Color.Empty, 0, 0);
                            CenterText(g, items[j], "mali", px, new PointF(mid.X, y + blur), A(Theme.Chalk, alpha / 4), Color.Empty, 0, 0);
                        }
                        CenterText(g, items[j], "mali", px, new PointF(mid.X, y), A(Theme.Chalk, alpha), Color.Empty, 0, 0);
                    }
                    // fade top and bottom into the slot shadow
                    var fadeT = new RectangleF(slot.X, slot.Y - 1, slot.Width, slot.Height * 0.3f);
                    using (var fb = new LinearGradientBrush(fadeT, Color.FromArgb(200, 30, 70, 54), Color.FromArgb(0, 30, 70, 54), LinearGradientMode.Vertical)) g.FillRectangle(fb, fadeT);
                    var fadeB = new RectangleF(slot.X, slot.Bottom - slot.Height * 0.3f, slot.Width, slot.Height * 0.3f + 1);
                    using (var fb = new LinearGradientBrush(fadeB, Color.FromArgb(0, 30, 66, 52), Color.FromArgb(200, 30, 66, 52), LinearGradientMode.Vertical)) g.FillRectangle(fb, fadeB);
                    g.Restore(cs);
                    using (var edge = new Pen(Color.FromArgb(70, 255, 255, 255), Math.Max(1, m * 0.004f))) g.DrawPath(edge, slotPath);
                }
                // pointers either side
                float ts = slot.Height * 0.14f;
                using (var tb = new SolidBrush(ChalkYellow))
                {
                    g.FillPolygon(tb, new[] { new PointF(slot.X - ts * 0.3f, mid.Y - ts), new PointF(slot.X + ts * 0.9f, mid.Y), new PointF(slot.X - ts * 0.3f, mid.Y + ts) });
                    g.FillPolygon(tb, new[] { new PointF(slot.Right + ts * 0.3f, mid.Y - ts), new PointF(slot.Right - ts * 0.9f, mid.Y), new PointF(slot.Right + ts * 0.3f, mid.Y + ts) });
                }
                // running lights along the bottom
                int dots = 9;
                for (int i = 0; i < dots; i++)
                {
                    bool on = ((int)(now * 10) + i) % 3 == 0;
                    float dx = board.X + board.Width * (0.2f + 0.6f * i / (dots - 1)), dy = board.Y + board.Height * 0.85f, dr = bm * 0.018f;
                    using (var db = new SolidBrush(on ? ChalkYellow : Color.FromArgb(70, 255, 255, 255))) g.FillEllipse(db, dx - dr, dy - dr, 2 * dr, 2 * dr);
                }
            }
            else
            {
                // winner: glow, name, chalk underline, stars and confetti
                FallingConfetti(g, board, Seed(name), 34, bm * 0.035f, now, 230);
                using (var glow = new GraphicsPath())
                {
                    glow.AddEllipse(mid.X - slot.Width * 0.5f, mid.Y - slot.Height * 0.55f, slot.Width, slot.Height * 1.1f);
                    using (var pg = new PathGradientBrush(glow) { CenterColor = Color.FromArgb(70, 255, 230, 140), SurroundColors = new[] { Color.FromArgb(0, 255, 230, 140) } })
                        g.FillPath(pg, glow);
                }
                float px = FitPx(name, "mali", namePxMax * 1.15f, slot.Width * 0.92f);
                RectangleF nb;
                using (TextPath(name, "mali", px, out nb)) { }
                CenterText(g, name, "mali", px, mid, Color.White, Color.FromArgb(120, 10, 40, 30), px * 0.08f, px * 0.05f);
                // chalk swoosh under the name
                float uw = Math.Min(slot.Width * 0.9f, nb.Width * 1.05f), uy = mid.Y + nb.Height * 0.62f + slot.Height * 0.04f;
                using (var sw = RPen(A(ChalkYellow, 230), Math.Max(2, px * 0.07f)))
                    g.DrawBezier(sw, mid.X - uw / 2, uy + px * 0.04f, mid.X - uw / 6, uy - px * 0.08f, mid.X + uw / 6, uy + px * 0.1f, mid.X + uw / 2, uy - px * 0.04f);
                for (int i = 0; i < 6; i++)
                {
                    float tw = (float)(0.5 + 0.5 * Math.Sin(now * 4 + i * 1.3));
                    float side = i % 2 == 0 ? -1 : 1;
                    float room = (board.Width - uw) / 2;
                    float sx, sy;
                    float starR = bm * 0.05f;
                    if (room > starR * 4) { sx = mid.X + side * (uw / 2 + Math.Max(starR * 1.6f, room * (0.35f + 0.15f * (i / 2)))); sy = mid.Y + (i / 2 - 1) * slot.Height * 0.42f; }
                    else { sx = mid.X + side * uw * (0.2f + 0.13f * (i / 2)); sy = mid.Y + (i / 2 == 1 ? 1 : -1) * (nb.Height * 0.5f + slot.Height * 0.32f); }
                    using (var s = Star(new PointF(sx, sy), bm * (0.03f + 0.02f * tw), 0.45f, 5, (float)(now * 40 + i * 20)))
                    using (var b = new SolidBrush(A(i % 3 == 0 ? ChalkPink : ChalkYellow, (int)(150 + 105 * tw)))) g.FillPath(b, s);
                }
                string bottom = "lên bảng nhé!";
                CenterText(g, bottom, "chalk", Math.Min(board.Height * 0.09f, FitPx(bottom, "chalk", board.Height * 0.09f, board.Width * 0.6f)), new PointF(mid.X, board.Y + board.Height * 0.86f), A(Theme.Chalk, 170), Color.Empty, 0, 0);
            }
            bpath.Dispose();
            g.Restore(st);
        }

        // ================================================================ GROUP CARD

        /// <summary>A group card: cream notebook paper with a coloured header and washi tape; members shrink to fit.</summary>
        public static void PaintGroupCard(Graphics g, RectangleF r, int index, string title, IList<string> members)
        {
            if (r.Width < 10 || r.Height < 10) return;
            var st = g.Save();
            Begin(g);
            Color col = GroupColor(index);
            float m = Math.Min(r.Width, r.Height);
            float tapeH = Clamp(m * 0.09f, 6, 26);
            var card = new RectangleF(r.X + 2, r.Y + tapeH * 0.45f, r.Width - 6, r.Height - tapeH * 0.45f - 6);
            float rad = Clamp(m * 0.05f, 4, 18);
            float headH = Clamp(card.Height * 0.2f, 20, 70);
            if (headH > card.Height * 0.45f) headH = card.Height * 0.45f;

            using (var cp = Round(card, rad))
            {
                SoftShadow(g, cp, 1.5f, Math.Max(2, m * 0.018f), 40);
                using (var pb = new SolidBrush(Theme.Paper)) g.FillPath(pb, cp);
                var cs = g.Save();
                g.SetClip(cp, CombineMode.Intersect);
                using (var hb = new LinearGradientBrush(new RectangleF(card.X, card.Y, card.Width, headH + 1), Mix(col, Color.White, 0.12f), Mix(col, Color.Black, 0.08f), LinearGradientMode.Vertical))
                    g.FillRectangle(hb, card.X, card.Y, card.Width, headH);
                // a scalloped bottom edge on the header, like a bunting flag
                int bumps = Math.Max(4, (int)(card.Width / (headH * 0.5f)));
                float bw = card.Width / bumps;
                using (var hb = new SolidBrush(Mix(col, Color.Black, 0.08f)))
                    for (int i = 0; i < bumps; i++) g.FillPie(hb, card.X + i * bw, card.Y + headH - bw / 2, bw, bw, 0, 180);
                g.Restore(cs);
                using (var edge = new Pen(Theme.LineStrong, 1)) g.DrawPath(edge, cp);
            }

            // number badge + title
            float bd = headH * 0.66f;
            var badge = new RectangleF(card.X + headH * 0.22f, card.Y + (headH - bd) / 2, bd, bd);
            using (var bb = new SolidBrush(Color.White)) g.FillEllipse(bb, badge);
            string num = (index + 1).ToString();
            CenterText(g, num, "uiBold", FitPx(num, "uiBold", bd * 0.62f, bd * 0.7f), new PointF(badge.X + bd / 2, badge.Y + bd / 2), col, Color.Empty, 0, 0);
            string t = string.IsNullOrEmpty(title) ? "Nhóm " + num : title;
            float tx = badge.Right + headH * 0.2f, tw = card.Right - tx - headH * 0.2f;
            float tpx = FitPx(t, "uiBold", headH * 0.46f, tw);
            RectangleF tbn;
            using (TextPath(t, "uiBold", tpx, out tbn)) { }
            CenterText(g, t, "uiBold", tpx, new PointF(tx + tbn.Width / 2, card.Y + headH / 2), Color.White, Color.Empty, 0, tpx * 0.06f);

            // washi tape
            var ts = g.Save();
            g.TranslateTransform(card.X + card.Width / 2, card.Y);
            g.RotateTransform(-3);
            float tapeW = Math.Min(card.Width * 0.36f, tapeH * 4.5f);
            using (var tb = new SolidBrush(Color.FromArgb(170, 255, 236, 160))) g.FillRectangle(tb, -tapeW / 2, -tapeH * 0.55f, tapeW, tapeH);
            using (var stripe = new Pen(Color.FromArgb(70, 230, 160, 40), Math.Max(1, tapeH * 0.12f)))
                for (float x = -tapeW / 2 + tapeH * 0.3f; x < tapeW / 2; x += tapeH * 0.6f) g.DrawLine(stripe, x, -tapeH * 0.55f, x + tapeH * 0.35f, tapeH * 0.45f);
            g.Restore(ts);

            // members on ruled lines
            var list = new List<string>();
            if (members != null) foreach (var s in members) if (!string.IsNullOrEmpty(s)) list.Add(s);
            var body = new RectangleF(card.X + rad * 0.6f, card.Y + headH + headH * 0.3f, card.Width - rad * 1.2f, card.Bottom - card.Y - headH - headH * 0.3f - rad * 0.6f);
            if (list.Count == 0)
            {
                CenterText(g, "(chưa có ai)", "chalk", Math.Max(8, Math.Min(body.Height * 0.25f, 26)), new PointF(body.X + body.Width / 2, body.Y + body.Height * 0.35f), Theme.Dim, Color.Empty, 0, 0);
                g.Restore(st);
                return;
            }
            if (body.Height < 6 || body.Width < 10) { g.Restore(st); return; }

            // choose 1 or 2 columns, whichever lets the names be bigger
            float best = 0; int bestCols = 1;
            for (int cols = 1; cols <= 2; cols++)
            {
                if (cols == 2 && list.Count < 6) break;
                int rows = (list.Count + cols - 1) / cols;
                float rowH = body.Height / rows, colW = body.Width / cols;
                float px = Math.Min(rowH * 0.62f, 40);
                foreach (var s in list) px = Math.Min(px, FitPx(s, "ui", px, colW - rowH * 0.7f - 4));
                if (px > best + 0.5f) { best = px; bestCols = cols; }
            }
            int nRows = (list.Count + bestCols - 1) / bestCols;
            float rh = body.Height / nRows, cw = body.Width / bestCols;
            rh = Math.Min(rh, best * 1.9f + 4);
            using (var rule = new Pen(Color.FromArgb(110, 150, 190, 220), 1))
                for (int i = 1; i <= nRows; i++) g.DrawLine(rule, body.X, body.Y + i * rh, body.Right, body.Y + i * rh);
            using (var f = Fonts.Get("ui", Math.Max(6, best)))
            using (var tbr = new SolidBrush(Theme.Ink))
            using (var dot = new SolidBrush(col))
            using (var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                for (int i = 0; i < list.Count; i++)
                {
                    int cI = i / nRows, rI = i % nRows;
                    float x = body.X + cI * cw, y = body.Y + rI * rh;
                    float dr = Math.Max(2, best * 0.18f);
                    g.FillEllipse(dot, x + rh * 0.25f - dr, y + rh * 0.55f - dr, 2 * dr, 2 * dr);
                    g.DrawString(list[i], f, tbr, new RectangleF(x + rh * 0.25f + dr * 2.2f, y + rh * 0.08f, cw - rh * 0.25f - dr * 2.2f, rh), sf);
                }
            }
            g.Restore(st);
        }

        // ================================================================ SCOREBOARD

        struct BoardLayout
        {
            public float ColW, Top, Floor, NameTop, NameH, BtnTop, BtnH, Btn;
        }

        static BoardLayout Layout(RectangleF r, int n)
        {
            n = Math.Max(1, n);
            var L = new BoardLayout();
            L.ColW = r.Width / n;
            L.BtnH = Clamp(r.Height * 0.12f, 22, 70);
            L.Btn = Math.Min(L.BtnH, L.ColW * 0.36f);
            L.BtnTop = r.Bottom - L.BtnH;
            L.NameH = Clamp(r.Height * 0.085f, 16, 52);
            L.NameTop = L.BtnTop - L.NameH - L.BtnH * 0.18f;
            L.Floor = L.NameTop - L.NameH * 0.25f;
            L.Top = r.Top;
            return L;
        }

        static RectangleF ScoreButton(RectangleF r, int teamCount, int i, bool plus)
        {
            var L = Layout(r, teamCount);
            float cx = r.X + L.ColW * (i + 0.5f);
            float gap = Math.Min(L.Btn * 0.25f, L.ColW * 0.06f);
            float x = plus ? cx + gap / 2 : cx - gap / 2 - L.Btn;
            return new RectangleF(x, L.BtnTop + (L.BtnH - L.Btn) / 2, L.Btn, L.Btn);
        }

        public static RectangleF ScorePlusRect(RectangleF r, int teamCount, int i) { return ScoreButton(r, teamCount, i, true); }
        public static RectangleF ScoreMinusRect(RectangleF r, int teamCount, int i) { return ScoreButton(r, teamCount, i, false); }

        static void Crown(Graphics g, PointF c, float w, double now)
        {
            float h = w * 0.62f;
            var st = g.Save();
            g.TranslateTransform(c.X, c.Y + (float)Math.Sin(now * 3) * w * 0.04f);
            g.RotateTransform((float)Math.Sin(now * 2.2) * 4);
            var pts = new[]
            {
                new PointF(-w / 2, h / 2), new PointF(-w / 2, -h * 0.25f), new PointF(-w * 0.25f, h * 0.05f), new PointF(0, -h / 2),
                new PointF(w * 0.25f, h * 0.05f), new PointF(w / 2, -h * 0.25f), new PointF(w / 2, h / 2)
            };
            using (var p = new GraphicsPath())
            {
                p.AddPolygon(pts);
                SoftShadow(g, p, w * 0.03f, w * 0.05f, 40);
                using (var b = new LinearGradientBrush(new RectangleF(-w / 2, -h / 2, w, h), Color.FromArgb(255, 224, 102), Gold, LinearGradientMode.Vertical)) g.FillPath(b, p);
                using (var e = new Pen(GoldDark, Math.Max(1, w * 0.035f)) { LineJoin = LineJoin.Round }) g.DrawPath(e, p);
            }
            using (var band = new SolidBrush(Color.FromArgb(120, GoldDark))) g.FillRectangle(band, -w / 2, h * 0.28f, w, h * 0.1f);
            using (var gem = new SolidBrush(Color.FromArgb(220, 50, 50))) g.FillEllipse(gem, -w * 0.07f, h * 0.03f, w * 0.14f, w * 0.14f);
            using (var gem = new SolidBrush(Color.FromArgb(50, 130, 220)))
            {
                g.FillEllipse(gem, -w * 0.33f, h * 0.13f, w * 0.1f, w * 0.1f);
                g.FillEllipse(gem, w * 0.23f, h * 0.13f, w * 0.1f, w * 0.1f);
            }
            using (var tip = new SolidBrush(Color.White))
                foreach (var p in new[] { pts[1], pts[3], pts[5] }) g.FillEllipse(tip, p.X - w * 0.045f, p.Y - w * 0.045f, w * 0.09f, w * 0.09f);
            g.Restore(st);
        }

        /// <summary>The team race: coloured columns on a wooden floor, the score on top, a crown for the leader, +/− buttons.</summary>
        public static void PaintScoreboard(Graphics g, RectangleF r, IList<string> teams, IList<int> scores, int highlight, double changedAt, double now)
        {
            int n = teams == null ? 0 : teams.Count;
            if (scores != null) n = Math.Max(n, scores.Count);
            if (n == 0 || r.Width < 20 || r.Height < 40) return;
            var st = g.Save();
            Begin(g);
            var L = Layout(r, n);
            Func<int, int> score = i => scores != null && i < scores.Count ? scores[i] : 0;
            int hi = int.MinValue, lo = int.MaxValue;
            for (int i = 0; i < n; i++) { hi = Math.Max(hi, score(i)); lo = Math.Min(lo, score(i)); }
            int leaders = 0;
            for (int i = 0; i < n; i++) if (score(i) == hi) leaders++;
            bool crown = hi > lo && hi > 0;
            float numPx = Clamp(Math.Min(L.ColW * 0.3f, r.Height * 0.11f), 10, 110);
            float crownW = Math.Min(L.ColW * 0.42f, numPx * 1.5f);
            float barTopMax = r.Top + crownW * 0.7f + numPx * 1.25f;
            float area = L.Floor - barTopMax;
            float minH = Math.Max(4, area * 0.06f);
            int span = Math.Max(1, Math.Max(hi, 0) - Math.Min(lo, 0));
            int baseV = Math.Min(lo, 0);

            // faint guide lines behind the columns
            using (var guide = new Pen(Color.FromArgb(40, 120, 100, 70), 1) { DashStyle = DashStyle.Dash })
                for (int k = 1; k <= 4; k++)
                {
                    float y = L.Floor - minH - (area - minH) * k / 4f;
                    g.DrawLine(guide, r.X + 4, y, r.Right - 4, y);
                }

            // wooden floor
            float floorH = Math.Max(4, L.NameH * 0.22f);
            using (var fp = Round(new RectangleF(r.X + 2, L.Floor - 1, r.Width - 4, floorH), floorH / 2))
            {
                using (var fb = new LinearGradientBrush(new RectangleF(r.X, L.Floor - 1, r.Width, floorH + 1), Theme.Wood, Theme.WoodDark, LinearGradientMode.Vertical)) g.FillPath(fb, fp);
            }

            for (int i = 0; i < n; i++)
            {
                Color col = GroupColor(i);
                int s = score(i);
                float cx = r.X + L.ColW * (i + 0.5f);
                float bw = Math.Max(6, L.ColW * 0.56f);
                float h = minH + (area - minH) * (s - baseV) / (float)span;
                var bar = new RectangleF(cx - bw / 2, L.Floor - h, bw, h);
                double age = i == highlight ? now - changedAt : 99;
                bool pop = age >= 0 && age < 1.2;

                using (var bp = new GraphicsPath())
                {
                    float rr = Math.Min(bw * 0.22f, h);
                    bp.AddArc(bar.X, bar.Y, rr * 2, rr * 2, 180, 90);
                    bp.AddArc(bar.Right - rr * 2, bar.Y, rr * 2, rr * 2, 270, 90);
                    bp.AddLine(bar.Right, bar.Bottom, bar.X, bar.Bottom);
                    bp.CloseFigure();
                    SoftShadow(g, bp, bw * 0.05f, 0, 35);
                    if (pop)
                    {
                        float k = (float)(1 - age / 1.2);
                        using (var gl = RPen(A(col, (int)(120 * k)), bw * 0.18f * k + 1)) g.DrawPath(gl, bp);
                    }
                    using (var b = new LinearGradientBrush(new RectangleF(bar.X - 1, bar.Y, bar.Width + 2, bar.Height + 1), Mix(col, Color.White, 0.18f), Mix(col, Color.Black, 0.12f), LinearGradientMode.Horizontal))
                        g.FillPath(b, bp);
                    // chalk stripes: one tally mark per row for a playful, hand-drawn feel
                    var cs = g.Save();
                    g.SetClip(bp, CombineMode.Intersect);
                    using (var shine = new SolidBrush(Color.FromArgb(60, 255, 255, 255))) g.FillRectangle(shine, bar.X + bw * 0.12f, bar.Y, bw * 0.12f, bar.Height);
                    using (var stripe = new Pen(Color.FromArgb(28, 255, 255, 255), Math.Max(1, bw * 0.06f)))
                        for (float y = bar.Bottom - bw * 0.3f; y > bar.Y; y -= bw * 0.3f) g.DrawLine(stripe, bar.X, y + bw * 0.12f, bar.Right, y - bw * 0.12f);
                    g.Restore(cs);
                }

                // score number above the column
                float scale = 1;
                if (pop)
                {
                    double t = age / 0.55;
                    scale = t < 1 ? 1 + 0.55f * (float)(Math.Sin(t * Math.PI) * (1 - t * 0.4)) : 1;
                }
                var sc = new PointF(cx, bar.Y - numPx * 0.7f);
                var ss = g.Save();
                g.TranslateTransform(sc.X, sc.Y);
                g.ScaleTransform(scale, scale);
                string txt = s.ToString();
                float px = FitPx(txt, "uiBold", numPx, L.ColW * 0.9f);
                CenterText(g, txt, "uiBold", px, PointF.Empty, s < 0 ? Theme.Red : Mix(col, Color.Black, 0.25f), Color.FromArgb(235, 255, 255, 255), px * 0.16f, px * 0.05f);
                g.Restore(ss);
                if (pop && age < 0.9)
                {
                    float t = (float)(age / 0.9);
                    for (int k = 0; k < 8; k++)
                    {
                        double a = k * Math.PI / 4 + 0.3;
                        float d = numPx * (0.5f + 1.1f * t);
                        Sparkle(g, new PointF(sc.X + (float)Math.Cos(a) * d, sc.Y + (float)Math.Sin(a) * d * 0.8f), numPx * 0.16f * (1 - t), A(k % 2 == 0 ? Gold : col, (int)(255 * (1 - t))));
                    }
                }
                if (crown && s == hi)
                    Crown(g, new PointF(cx, sc.Y - numPx * 0.62f - crownW * 0.42f), crownW, now + i * 0.7);

                // team name in a pill
                string nm = teams != null && i < teams.Count && !string.IsNullOrEmpty(teams[i]) ? teams[i] : "Đội " + (i + 1);
                float pillW = L.ColW * 0.9f;
                var pill = new RectangleF(cx - pillW / 2, L.NameTop + L.NameH * 0.1f, pillW, L.NameH * 0.9f);
                using (var pp = Round(pill, pill.Height / 2))
                {
                    using (var pb = new SolidBrush(Color.FromArgb(245, 251, 248, 239))) g.FillPath(pb, pp);
                    using (var pe = new Pen(col, Math.Max(1.5f, pill.Height * 0.07f))) g.DrawPath(pe, pp);
                }
                float npx = FitPx(nm, "uiBold", pill.Height * 0.56f, pill.Width - pill.Height * 0.6f);
                CenterText(g, nm, "uiBold", npx, new PointF(cx, pill.Y + pill.Height / 2), Theme.Ink, Color.Empty, 0, 0);

                // − and + buttons
                var minus = ScoreButton(r, n, i, false);
                var plus = ScoreButton(r, n, i, true);
                float lw = Math.Max(2, minus.Width * 0.11f);
                using (var mp = new GraphicsPath())
                {
                    mp.AddEllipse(minus);
                    SoftShadow(g, mp, 0, minus.Height * 0.05f, 30);
                    using (var b = new SolidBrush(Color.White)) g.FillPath(b, mp);
                    using (var e = new Pen(col, Math.Max(1.5f, minus.Width * 0.06f))) g.DrawEllipse(e, RectangleF.Inflate(minus, -minus.Width * 0.03f, -minus.Width * 0.03f));
                }
                using (var p = RPen(col, lw))
                    g.DrawLine(p, minus.X + minus.Width * 0.3f, minus.Y + minus.Height / 2, minus.Right - minus.Width * 0.3f, minus.Y + minus.Height / 2);
                using (var pp = new GraphicsPath())
                {
                    pp.AddEllipse(plus);
                    SoftShadow(g, pp, 0, plus.Height * 0.05f, 40);
                    using (var b = new LinearGradientBrush(plus, Mix(col, Color.White, 0.15f), Mix(col, Color.Black, 0.1f), LinearGradientMode.Vertical)) g.FillPath(b, pp);
                }
                using (var p = RPen(Color.White, lw))
                {
                    g.DrawLine(p, plus.X + plus.Width * 0.28f, plus.Y + plus.Height / 2, plus.Right - plus.Width * 0.28f, plus.Y + plus.Height / 2);
                    g.DrawLine(p, plus.X + plus.Width / 2, plus.Y + plus.Height * 0.28f, plus.X + plus.Width / 2, plus.Bottom - plus.Height * 0.28f);
                }
            }
            g.Restore(st);
        }

        // ================================================================ GEOMETRY INSTRUMENTS

        static readonly Color TickInk = Color.FromArgb(240, 40, 34, 26);

        /// <summary>Wooden ruler from (0,0) to (lengthPx, widthPx); the measuring edge is y = 0, 0 cm at x = 0.</summary>
        public static void PaintRuler(Graphics g, float lengthPx, float widthPx, float pxPerCm)
        {
            if (lengthPx < 4 || widthPx < 4) return;
            var st = g.Save();
            Begin(g);
            float L = lengthPx, W = widthPx;
            if (pxPerCm < 2) pxPerCm = 2;
            var body = new RectangleF(0, 0, L, W);
            float rad = Math.Min(W * 0.08f, 8);
            using (var p = Round(body, rad))
            {
                SoftShadow(g, p, W * 0.04f + 1, W * 0.07f + 2, 34);
                using (var wood = new LinearGradientBrush(new RectangleF(0, -1, L, W + 2), Color.FromArgb(215, 244, 214, 158), Color.FromArgb(215, 224, 178, 108), LinearGradientMode.Vertical))
                    g.FillPath(wood, p);
                var cs = g.Save();
                g.SetClip(p, CombineMode.Intersect);
                using (var grain = new Pen(Color.FromArgb(34, 120, 70, 20), Math.Max(1, W * 0.012f)))
                    for (int i = 0; i < 7; i++)
                    {
                        float y = W * (0.18f + 0.12f * i) + (Rnd(41, i) - 0.5f) * W * 0.06f;
                        float amp = W * 0.03f * (Rnd(43, i) + 0.3f);
                        int segs = Math.Max(2, (int)(L / (W * 2.5f)));
                        var pts = new PointF[segs + 1];
                        for (int k = 0; k <= segs; k++) pts[k] = new PointF(L * k / segs, y + (float)Math.Sin(k * 1.7 + i) * amp);
                        g.DrawCurve(grain, pts, 0.5f);
                    }
                // bevel along the measuring edge
                using (var bevel = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillRectangle(bevel, 0, 0, L, W * 0.06f);
                using (var under = new SolidBrush(Color.FromArgb(40, 90, 50, 10))) g.FillRectangle(under, 0, W * 0.92f, L, W * 0.08f);
                g.Restore(cs);
                using (var e = new Pen(Color.FromArgb(200, 150, 100, 40), 1)) g.DrawPath(e, p);
            }
            using (var edge = new Pen(Color.FromArgb(230, 90, 55, 15), 1.2f)) g.DrawLine(edge, 0, 0.5f, L, 0.5f);

            // ticks
            float mm = pxPerCm / 10f;
            int step = mm >= 2.5f ? 1 : mm * 5 >= 2.5f ? 5 : 10;
            float thin = Clamp(pxPerCm * 0.022f, 1f, 2f);
            using (var tk = new Pen(TickInk, thin))
            using (var tkCm = new Pen(TickInk, thin * 1.5f))
                for (int i = 0; ; i += step)
                {
                    float x = i * mm;
                    if (x > L - 0.5f) break;
                    float len = i % 10 == 0 ? W * 0.34f : i % 5 == 0 ? W * 0.24f : W * 0.15f;
                    g.DrawLine(i % 10 == 0 ? tkCm : tk, x, 0, x, len);
                }
            float npx = Clamp(pxPerCm * 0.36f, 8, W * 0.26f);
            int every = pxPerCm >= npx * 1.6f ? 1 : pxPerCm * 2 >= npx * 1.6f ? 2 : 5;
            using (var f = Fonts.Get("uiSemi", npx))
            using (var b = new SolidBrush(TickInk))
            using (var sf = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center })
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                for (int cm = 0; ; cm += every)
                {
                    float x = cm * pxPerCm;
                    if (x > L - npx * 0.4f) break;
                    string s = cm.ToString();
                    var sz = g.MeasureString(s, f, PointF.Empty, sf);
                    float xx = cm == 0 ? Math.Max(x, sz.Width / 2 + W * 0.05f) : x;
                    if (xx + sz.Width / 2 > L - 2) break;
                    g.DrawString(s, f, b, new PointF(xx, W * 0.38f), sf);
                }
            }
            // printed marks: unit and brand
            if (W >= 30)
            {
                float bpx = Clamp(W * 0.14f, 7, 20);
                Label(g, "cm", "uiSemi", bpx, Color.FromArgb(170, 90, 55, 15), new RectangleF(W * 0.08f, W * 0.62f, W * 2, W * 0.36f), StringAlignment.Near, StringAlignment.Center);
                if (L > W * 5)
                    Label(g, "PHẤN TRẮNG", "uiBold", bpx, Color.FromArgb(110, 120, 70, 20), new RectangleF(L - W * 4.2f, W * 0.62f, W * 4f, W * 0.36f), StringAlignment.Far, StringAlignment.Center);
            }
            g.Restore(st);
        }

        /// <summary>Clear plastic half-circle protractor: centre at (0,0), arc above (negative y), two scales.</summary>
        public static void PaintProtractor(Graphics g, float radiusPx)
        {
            if (radiusPx < 6) return;
            var st = g.Save();
            Begin(g);
            float R = radiusPx, strip = R * 0.085f;
            using (var p = new GraphicsPath())
            {
                p.AddArc(-R, -R, 2 * R, 2 * R, 180, 180);
                p.AddLine(R, 0, R, strip);
                p.AddLine(R, strip, -R, strip);
                p.CloseFigure();
                SoftShadow(g, p, R * 0.02f + 1, R * 0.035f + 2, 32);
                using (var pg = new PathGradientBrush(p))
                {
                    pg.CenterPoint = new PointF(0, 0);
                    pg.CenterColor = Color.FromArgb(200, 236, 246, 252);
                    pg.SurroundColors = new[] { Color.FromArgb(212, 184, 222, 246) };
                    g.FillPath(pg, p);
                }
                // inner window: clearer
                using (var win = new GraphicsPath())
                {
                    float ri = R * 0.36f;
                    win.AddArc(-ri, -ri, 2 * ri, 2 * ri, 180, 180);
                    win.CloseFigure();
                    using (var wb = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillPath(wb, win);
                    using (var we = new Pen(Color.FromArgb(150, 70, 120, 160), Math.Max(1, R * 0.004f))) g.DrawPath(we, win);
                }
                // glossy highlight
                using (var gl = RPen(Color.FromArgb(90, 255, 255, 255), R * 0.02f)) g.DrawArc(gl, -R * 0.95f, -R * 0.95f, R * 1.9f, R * 1.9f, 200, 50);
                using (var e = new Pen(Color.FromArgb(220, 60, 110, 150), Math.Max(1, R * 0.006f))) g.DrawPath(e, p);
            }

            Color inner = Color.FromArgb(235, 200, 40, 40);
            float thin = Clamp(R * 0.0035f, 0.8f, 1.6f);
            // faint radial guides every 10°
            using (var guide = new Pen(Color.FromArgb(55, 40, 80, 120), thin))
                for (int a = 10; a < 180; a += 10)
                {
                    double t = a * Math.PI / 180;
                    float co = (float)Math.Cos(t), si = (float)Math.Sin(t);
                    g.DrawLine(guide, co * R * 0.38f, -si * R * 0.38f, co * R * 0.6f, -si * R * 0.6f);
                }
            using (var tk = new Pen(TickInk, thin))
            using (var tk10 = new Pen(TickInk, thin * 1.6f))
                for (int a = 0; a <= 180; a++)
                {
                    if (R < 60 && a % 5 != 0) continue;
                    double t = a * Math.PI / 180;
                    float co = (float)Math.Cos(t), si = (float)Math.Sin(t);
                    float len = a % 10 == 0 ? R * 0.09f : a % 5 == 0 ? R * 0.06f : R * 0.035f;
                    float r1 = R * 0.995f, r2 = r1 - len;
                    g.DrawLine(a % 10 == 0 ? tk10 : tk, co * r1, -si * r1, co * r2, -si * r2);
                    // inner ring ticks
                    if (a % 10 == 0) g.DrawLine(tk, co * R * 0.62f, -si * R * 0.62f, co * R * 0.655f, -si * R * 0.655f);
                }
            using (var ring = new Pen(Color.FromArgb(150, 40, 34, 26), thin)) g.DrawArc(ring, -R * 0.62f, -R * 0.62f, R * 1.24f, R * 1.24f, 180, 180);

            // numbers: outer scale 0 at left, inner (red) scale 0 at right
            float opx = Clamp(R * 0.058f, 6, 30), ipx = Clamp(R * 0.046f, 5, 24);
            int every = R < 110 ? 30 : 10;
            using (var fo = Fonts.Get("uiSemi", opx))
            using (var fi = Fonts.Get("uiSemi", ipx))
            using (var bo = new SolidBrush(TickInk))
            using (var bi = new SolidBrush(inner))
            using (var sf = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                for (int a = 0; a <= 180; a += every)
                {
                    double t = a * Math.PI / 180;
                    float co = (float)Math.Cos(t), si = (float)Math.Sin(t);
                    float ro = R * 0.84f, ri = R * 0.72f;
                    var ts = g.Save();
                    g.TranslateTransform(co * ro, -si * ro);
                    g.RotateTransform(90 - a);
                    g.DrawString((180 - a).ToString(), fo, bo, PointF.Empty, sf);
                    g.Restore(ts);
                    ts = g.Save();
                    g.TranslateTransform(co * ri, -si * ri);
                    g.RotateTransform(90 - a);
                    g.DrawString(a.ToString(), fi, bi, PointF.Empty, sf);
                    g.Restore(ts);
                }
            }
            // baseline and centre
            using (var bl = new Pen(TickInk, thin * 1.4f)) g.DrawLine(bl, -R, 0, R, 0);
            using (var cl = new Pen(TickInk, thin))
            {
                g.DrawLine(cl, 0, -R * 0.12f, 0, strip * 0.8f);
                float cr = Math.Max(3, R * 0.025f);
                g.DrawEllipse(cl, -cr, -cr, 2 * cr, 2 * cr);
            }
            if (R >= 120)
                CenterText(g, "PHẤN TRẮNG", "uiBold", Clamp(R * 0.036f, 6, 16), new PointF(0, -R * 0.2f), Color.FromArgb(110, 50, 90, 130), Color.Empty, 0, 0);
            g.Restore(st);
        }

        /// <summary>Clear amber set square: right angle at (0,0), one leg along +x, the other up (−y); cm ticks on the x leg.</summary>
        public static void PaintSetSquare(Graphics g, float legPx, float pxPerCm, string kind)
        {
            if (legPx < 8) return;
            var st = g.Save();
            Begin(g);
            if (pxPerCm < 2) pxPerCm = 2;
            float a = legPx, b = legPx * (kind == "30" ? 0.577f : 1f);
            var O = new PointF(0, 0);
            var X = new PointF(a, 0);
            var Y = new PointF(0, -b);
            float c = (float)Math.Sqrt(a * a + b * b);
            float rin = (a + b - c) / 2;
            var I = new PointF(rin, -rin);
            float inset = Math.Max(pxPerCm * 0.95f, Math.Min(a, b) * 0.14f);
            float k = 1 - inset / rin;
            using (var p = new GraphicsPath(FillMode.Alternate))
            {
                p.AddPolygon(new[] { O, X, Y });
                SoftShadow(g, p, legPx * 0.015f + 1, legPx * 0.03f + 2, 32);
                if (k > 0.18f)
                {
                    Func<PointF, PointF> sc = v => new PointF(I.X + (v.X - I.X) * k, I.Y + (v.Y - I.Y) * k);
                    p.AddPolygon(new[] { sc(O), sc(X), sc(Y) });
                }
                using (var fill = new LinearGradientBrush(new RectangleF(0, -b - 1, a + 1, b + 2), Color.FromArgb(212, 255, 226, 140), Color.FromArgb(205, 250, 196, 90), 45f))
                    g.FillPath(fill, p);
                using (var e = new Pen(Color.FromArgb(220, 170, 110, 20), Math.Max(1, legPx * 0.004f)) { LineJoin = LineJoin.Round }) g.DrawPath(e, p);
            }
            // glossy streak along the hypotenuse
            using (var gl = RPen(Color.FromArgb(80, 255, 255, 255), Math.Max(1.5f, legPx * 0.012f)))
            {
                float t0 = 0.12f, t1 = 0.5f, off = Math.Min(inset * 0.35f, legPx * 0.03f);
                float nx = -b / c, ny = a / c; // inward normal of the hypotenuse (towards the right angle)
                g.DrawLine(gl, X.X + (Y.X - X.X) * t0 + nx * off, X.Y + (Y.Y - X.Y) * t0 + ny * off, X.X + (Y.X - X.X) * t1 + nx * off, X.Y + (Y.Y - X.Y) * t1 + ny * off);
            }
            using (var edge = new Pen(Color.FromArgb(230, 120, 70, 10), 1.2f)) g.DrawLine(edge, 0, -0.5f, a, -0.5f);

            // ticks on the x leg, pointing up into the plastic, stopping before the hypotenuse
            float mm = pxPerCm / 10f, thin = Clamp(pxPerCm * 0.022f, 1f, 2f);
            int step = mm >= 2.5f ? 1 : mm * 5 >= 2.5f ? 5 : 10;
            float bandMax = pxPerCm * 0.36f;
            using (var tk = new Pen(TickInk, thin))
            using (var tkCm = new Pen(TickInk, thin * 1.5f))
                for (int i = 0; ; i += step)
                {
                    float x = i * mm;
                    if (x > a) break;
                    float room = b * (1 - x / a);
                    float len = i % 10 == 0 ? bandMax : i % 5 == 0 ? bandMax * 0.7f : bandMax * 0.42f;
                    if (len > room * 0.8f) break;
                    g.DrawLine(i % 10 == 0 ? tkCm : tk, x, 0, x, -len);
                }
            float npx = Clamp(pxPerCm * 0.3f, 7, 22);
            using (var f = Fonts.Get("uiSemi", npx))
            using (var br = new SolidBrush(TickInk))
            using (var sf = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center })
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                int every = pxPerCm >= npx * 1.6f ? 1 : 2;
                for (int cm = 1; ; cm += every)
                {
                    float x = cm * pxPerCm;
                    float room = b * (1 - x / a);
                    if (x > a || room < bandMax + npx * 1.6f) break;
                    g.DrawString(cm.ToString(), f, br, new PointF(x, -bandMax - npx * 1.2f), sf);
                }
            }
            // right-angle mark in the corner of the window
            if (k > 0.18f)
            {
                var o2 = new PointF(I.X * (1 - k), I.Y * (1 - k));
                float q = Math.Min(rin * k * 0.3f, pxPerCm * 0.5f);
                using (var ra = new Pen(Color.FromArgb(210, 190, 60, 30), Math.Max(1, thin * 1.2f)))
                    g.DrawLines(ra, new[] { new PointF(o2.X + q, o2.Y), new PointF(o2.X + q, o2.Y - q), new PointF(o2.X, o2.Y - q) });
            }
            g.Restore(st);
        }

        /// <summary>A compass: needle at pivot, chalk holder at pencil, brass hinge above the middle.</summary>
        public static void PaintCompass(Graphics g, PointF pivot, PointF pencil, bool drawing)
        {
            var st = g.Save();
            Begin(g);
            float dx = pencil.X - pivot.X, dy = pencil.Y - pivot.Y;
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            float ux = d > 0.5f ? dx / d : 1, uy = d > 0.5f ? dy / d : 0;
            float nx = -uy, ny = ux;
            if (ny > 0 || (Math.Abs(ny) < 1e-4f && nx > 0)) { nx = -nx; ny = -ny; }
            float Lg = Math.Max(150, d * 0.58f);
            float hh = (float)Math.Sqrt(Math.Max(0, Lg * Lg - d * d / 4));
            var mid = new PointF((pivot.X + pencil.X) / 2, (pivot.Y + pencil.Y) / 2);
            var H = new PointF(mid.X + nx * hh, mid.Y + ny * hh);
            float w = Clamp(Lg * 0.075f, 8, 18);

            Func<PointF, PointF, float, float, float, float, PointF[]> bar = (a, b, w1, w2, t0, t1) =>
            {
                float bx = b.X - a.X, by = b.Y - a.Y, bl = (float)Math.Sqrt(bx * bx + by * by);
                if (bl < 0.01f) bl = 0.01f;
                float px = -by / bl, py = bx / bl;
                var s = new PointF(a.X + bx * t0, a.Y + by * t0);
                var e = new PointF(a.X + bx * t1, a.Y + by * t1);
                return new[] { new PointF(s.X + px * w1 / 2, s.Y + py * w1 / 2), new PointF(e.X + px * w2 / 2, e.Y + py * w2 / 2), new PointF(e.X - px * w2 / 2, e.Y - py * w2 / 2), new PointF(s.X - px * w1 / 2, s.Y - py * w1 / 2) };
            };
            Func<PointF, PointF, float, PointF> at = (a, b, t) => new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

            var legN = bar(H, pivot, w, w * 0.7f, 0, 0.86f);
            var needle = bar(H, pivot, w * 0.22f, 0.6f, 0.86f, 1f);
            var legP = bar(H, pencil, w, w * 0.75f, 0, 0.7f);
            var clamp = bar(H, pencil, w * 1.35f, w * 1.35f, 0.66f, 0.76f);
            var chalk = bar(H, pencil, w * 0.62f, w * 0.62f, 0.72f, 0.965f);
            var tipC = bar(H, pencil, w * 0.62f, w * 0.16f, 0.965f, 1f);

            // shadow of the whole instrument
            var sh = g.Save();
            g.TranslateTransform(5, 8);
            using (var sb = new SolidBrush(Color.FromArgb(38, 0, 0, 0)))
            {
                foreach (var poly in new[] { legN, needle, legP, clamp, chalk, tipC }) g.FillPolygon(sb, poly);
                g.FillEllipse(sb, H.X - w * 1.1f, H.Y - w * 1.1f, w * 2.2f, w * 2.2f);
            }
            g.Restore(sh);

            Action<PointF[], Color, Color> metal = (poly, c1, c2) =>
            {
                var bounds = new RectangleF(poly.Min(p => p.X) - 1, poly.Min(p => p.Y) - 1, poly.Max(p => p.X) - poly.Min(p => p.X) + 2, poly.Max(p => p.Y) - poly.Min(p => p.Y) + 2);
                float ang = (float)(Math.Atan2(poly[1].Y - poly[0].Y, poly[1].X - poly[0].X) * 180 / Math.PI) + 90;
                using (var b = new LinearGradientBrush(bounds, c1, c2, ang)) g.FillPolygon(b, poly);
                using (var e = new Pen(Color.FromArgb(150, 60, 64, 70), 1) { LineJoin = LineJoin.Round }) g.DrawPolygon(e, poly);
            };
            Color steel1 = Color.FromArgb(222, 236, 240, 244), steel2 = Color.FromArgb(222, 132, 140, 150);
            metal(needle, Color.FromArgb(235, 210, 214, 220), Color.FromArgb(235, 90, 96, 104));
            metal(legN, steel1, steel2);
            metal(legP, steel1, steel2);
            // the chalk stick
            using (var cb = new SolidBrush(Color.FromArgb(240, 250, 249, 242))) { g.FillPolygon(cb, chalk); g.FillPolygon(cb, tipC); }
            using (var ce = new Pen(Color.FromArgb(150, 150, 140, 120), 1)) { g.DrawPolygon(ce, chalk); g.DrawPolygon(ce, tipC); }
            metal(clamp, Color.FromArgb(230, 240, 120, 90), Color.FromArgb(230, 180, 50, 40));
            // screw on the clamp
            var scr = at(H, pencil, 0.71f);
            using (var sb = new SolidBrush(Color.FromArgb(240, 250, 210, 120))) g.FillEllipse(sb, scr.X - w * 0.22f, scr.Y - w * 0.22f, w * 0.44f, w * 0.44f);

            // grip handle above the hinge
            var gripEnd = new PointF(H.X + nx * w * 2.6f, H.Y + ny * w * 2.6f);
            var grip = bar(H, gripEnd, w * 0.75f, w * 0.75f, 0.2f, 1f);
            metal(grip, Color.FromArgb(240, 240, 120, 90), Color.FromArgb(240, 170, 45, 35));
            using (var knurl = new Pen(Color.FromArgb(70, 255, 255, 255), 1))
                for (int i = 1; i < 5; i++)
                {
                    var a0 = at(H, gripEnd, 0.25f + i * 0.15f);
                    g.DrawLine(knurl, a0.X - ux * w * 0.33f, a0.Y - uy * w * 0.33f, a0.X + ux * w * 0.33f, a0.Y + uy * w * 0.33f);
                }
            // brass hinge
            float hr = w * 1.05f;
            using (var hp = new GraphicsPath())
            {
                hp.AddEllipse(H.X - hr, H.Y - hr, 2 * hr, 2 * hr);
                using (var pg = new PathGradientBrush(hp) { CenterPoint = new PointF(H.X - hr * 0.35f, H.Y - hr * 0.35f), CenterColor = Color.FromArgb(250, 255, 236, 150), SurroundColors = new[] { Color.FromArgb(250, 190, 130, 30) } })
                    g.FillPath(pg, hp);
                using (var e = new Pen(Color.FromArgb(200, 130, 85, 15), 1)) g.DrawPath(e, hp);
            }
            using (var dot = new SolidBrush(Color.FromArgb(200, 120, 80, 20))) g.FillEllipse(dot, H.X - hr * 0.25f, H.Y - hr * 0.25f, hr * 0.5f, hr * 0.5f);

            // needle point mark
            using (var pm = new Pen(Color.FromArgb(160, 60, 60, 60), 1)) g.DrawEllipse(pm, pivot.X - 2.5f, pivot.Y - 2.5f, 5, 5);
            if (drawing)
            {
                float gr = w * 1.4f;
                using (var gp = new GraphicsPath())
                {
                    gp.AddEllipse(pencil.X - gr, pencil.Y - gr, 2 * gr, 2 * gr);
                    using (var pg = new PathGradientBrush(gp) { CenterColor = Color.FromArgb(220, 255, 250, 210), SurroundColors = new[] { Color.FromArgb(0, 255, 230, 120) } })
                        g.FillPath(pg, gp);
                }
                using (var cd = new SolidBrush(Color.White)) g.FillEllipse(cd, pencil.X - w * 0.22f, pencil.Y - w * 0.22f, w * 0.44f, w * 0.44f);
            }
            g.Restore(st);
        }
    }
}
