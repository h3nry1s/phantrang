using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace PhanTrang
{
    /// <summary>Line icons for the toolbar, drawn in code so they stay crisp at any size.</summary>
    public static class ToolIcons
    {
        public static void Draw(Graphics g, string icon, Rectangle r, Color col, bool onDark)
        {
            if (ClassArt.Icon(g, icon, r, col, onDark)) return;   // the classroom tools have their own, more playful icons
            float u = r.Width / 24f, cx = r.Left + r.Width / 2f, cy = r.Top + r.Height / 2f;
            var st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            string kind = icon, arg = "";
            int colon = icon.IndexOf(':');
            if (colon >= 0) { kind = icon.Substring(0, colon); arg = icon.Substring(colon + 1); }
            // whole-pixel stroke on whole-pixel points: no grey half-pixel edges
            using (var pen = new Pen(col, Math.Max(2f, (float)Math.Round(1.9f * u))) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var brush = new SolidBrush(col))
            {
                Func<float, float, PointF> P = (x, y) => new PointF((float)Math.Round(r.Left + x * u), (float)Math.Round(r.Top + y * u));
                switch (kind)
                {
                    case "pointer":
                        Pointers.DrawIcon(g, arg, new RectangleF(r.X - 2 * u, r.Y - 2 * u, r.Width + 4 * u, r.Height + 4 * u));
                        break;
                    case "type":
                        g.DrawRectangle(pen, r.Left + 2 * u, r.Top + 6 * u, 20 * u, 12 * u);
                        foreach (var x in new[] { 6f, 10f, 14f, 18f }) g.FillEllipse(brush, r.Left + x * u - 0.9f * u, r.Top + 9.2f * u, 1.8f * u, 1.8f * u);
                        g.DrawLine(pen, P(7, 14.5f), P(17, 14.5f));
                        break;
                    case "shape":
                        if (arg == "line") g.DrawLine(pen, P(4, 20), P(20, 4));
                        else if (arg == "rect") g.DrawRectangle(pen, r.Left + 3 * u, r.Top + 6 * u, 18 * u, 12 * u);
                        else if (arg == "ellipse") g.DrawEllipse(pen, r.Left + 3 * u, r.Top + 4 * u, 18 * u, 16 * u);
                        else { g.DrawLine(pen, P(4, 20), P(19, 5)); g.DrawLines(pen, new[] { P(11, 5), P(19, 5), P(19, 13) }); }
                        break;
                    case "stamp":
                        g.Restore(st);
                        StampMark.DrawAt(g, arg, new PointF(cx, cy), arg == "totlam" ? r.Width * 0.66f : r.Width * 1.05f, 1);
                        return;
                    case "draw":
                        g.DrawPolygon(pen, new[] { P(4, 20), P(5, 15.5f), P(16, 4.5f), P(19.5f, 8), P(8.5f, 19) });
                        g.DrawLine(pen, P(14, 6.5f), P(17.5f, 10));
                        break;
                    case "hl":
                        using (var hb = new Pen(Color.FromArgb(220, 242, 194, 48), 4.5f * u)) g.DrawLine(hb, P(3, 21), P(21, 21));
                        g.DrawPolygon(pen, new[] { P(9, 15), P(6, 18), P(11, 18) });
                        g.DrawPolygon(pen, new[] { P(9, 15), P(17, 7), P(20, 10), P(12, 18) });
                        break;
                    case "vanish":
                        g.DrawPolygon(pen, new[] { P(10, 18), P(11, 14), P(19, 6), P(22, 9), P(14, 17) });
                        for (int d = 0; d < 3; d++)
                            using (var dot = new SolidBrush(Color.FromArgb(220 - d * 70, col)))
                                g.FillEllipse(dot, P(6.5f - d * 2.2f, 19 + d * 0.6f).X - 1.6f * u, P(6.5f - d * 2.2f, 19 + d * 0.6f).Y - 1.6f * u, 3.2f * u, 3.2f * u);
                        break;
                    case "power":
                        using (var red = new Pen(onDark ? col : Theme.Red, Math.Max(2f, (float)Math.Round(2.1f * u))) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        {
                            var pr = new RectangleF(P(5, 6).X, P(5, 6).Y, 14 * u, 14 * u);
                            g.DrawArc(red, pr, -55, 290);
                            g.DrawLine(red, P(12, 3), P(12, 12));
                        }
                        break;
                    case "eraser":
                        g.DrawPolygon(pen, new[] { P(5, 15), P(13, 7), P(18, 12), P(13, 17), P(8, 17) });
                        g.DrawLine(pen, P(8, 20), P(20, 20));
                        break;
                    case "color":
                        var c = Catalog.Hex(arg);
                        var cr = new RectangleF(r.Left + 3 * u, r.Top + 3 * u, 18 * u, 18 * u);
                        using (var b = new SolidBrush(c)) g.FillEllipse(b, cr);
                        using (var ring = new Pen(onDark ? Theme.Chalk : Theme.Line, 1.5f * u)) g.DrawEllipse(ring, cr);
                        break;
                    case "width":
                        float w = arg == "thin" ? 1.4f : arg == "thick" ? 5.5f : 3f;
                        using (var wp = new Pen(col, w * u) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                            g.DrawBezier(wp, P(3, 15), P(9, 4), P(14, 20), P(21, 9));
                        break;
                    case "undo":
                        g.DrawLines(pen, new[] { P(9, 14), P(4, 9), P(9, 4) });
                        g.DrawBezier(pen, P(4, 9), P(14, 9), P(20, 9), P(20, 14.5f));
                        g.DrawBezier(pen, P(20, 14.5f), P(20, 18), P(17, 20), P(13, 20));
                        break;
                    case "redo":
                        g.DrawLines(pen, new[] { P(15, 14), P(20, 9), P(15, 4) });
                        g.DrawBezier(pen, P(20, 9), P(10, 9), P(4, 9), P(4, 14.5f));
                        g.DrawBezier(pen, P(4, 14.5f), P(4, 18), P(7, 20), P(11, 20));
                        break;
                    case "clear":
                        g.DrawLine(pen, P(3, 6), P(21, 6));
                        g.DrawLines(pen, new[] { P(8, 6), P(8, 4), P(16, 4), P(16, 6) });
                        g.DrawLines(pen, new[] { P(6, 6), P(7, 20), P(17, 20), P(18, 6) });
                        break;
                    case "board":
                        var br = new RectangleF(r.Left + 2 * u, r.Top + 3 * u, 20 * u, 14 * u);
                        if (arg == "" || arg == "none")
                        {
                            g.DrawRectangle(pen, br.X, br.Y, br.Width, br.Height);
                            g.DrawLine(pen, P(8, 21), P(16, 21));
                            g.DrawLine(pen, P(12, 17), P(12, 21));
                            break;
                        }
                        Paper.Paint(g, arg, Rectangle.Round(br), 120);
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        using (var edge = new Pen(onDark ? Theme.Chalk : Theme.Text, 1.3f * u)) g.DrawRectangle(edge, br.X, br.Y, br.Width, br.Height);
                        g.DrawLine(pen, P(8, 21), P(16, 21));
                        g.DrawLine(pen, P(12, 17), P(12, 21));
                        break;
                    case "swatch":
                        var sw = new Rectangle((int)(r.Left + 1 * u), (int)(r.Top + 4 * u), (int)(22 * u), (int)(16 * u));
                        if (arg == "none")
                        {
                            using (var dash = new Pen(col, Math.Max(1.5f, 1.5f * u)) { DashStyle = DashStyle.Dash }) g.DrawRectangle(dash, sw);
                            g.DrawLine(pen, P(5, 17), P(19, 7));
                            break;
                        }
                        using (var clip = PhanTrang.Draw.Round(sw, 3 * u))
                        {
                            var cs = g.Save();
                            g.SetClip(clip);
                            Paper.Paint(g, arg, sw, 70);
                            g.Restore(cs);
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            using (var edge = new Pen(onDark ? Theme.Chalk : Theme.LineStrong, 1)) g.DrawPath(edge, clip);
                        }
                        break;
                    case "mode":
                        if (arg == "mouse")
                            g.DrawPolygon(pen, new[] { P(6, 3), P(6, 19), P(10, 15), P(13, 21), P(15.5f, 20), P(12.5f, 14), P(18, 14) });
                        else if (arg == "point")
                        {
                            // a stick of chalk touching the board, with a little tap mark
                            g.DrawPolygon(pen, new[] { P(9, 21), P(5.5f, 17.5f), P(15.5f, 7.5f), P(19, 11) });
                            g.DrawLine(pen, P(12.5f, 10.5f), P(16, 14));
                            g.DrawLine(pen, P(19.5f, 3), P(19.5f, 5));
                            g.DrawLine(pen, P(21.5f, 6), P(23, 5));
                            g.DrawLine(pen, P(16.5f, 4), P(15.5f, 2.5f));
                        }
                        else if (arg == "write")
                        {
                            g.DrawPolygon(pen, new[] { P(4, 20), P(5, 15.5f), P(16, 4.5f), P(19.5f, 8), P(8.5f, 19) });
                            g.DrawLine(pen, P(14, 6.5f), P(17.5f, 10));
                        }
                        else
                        {
                            g.DrawEllipse(pen, r.Left + 5.5f * u, r.Top + 3 * u, 13 * u, 13 * u);
                            g.DrawLines(pen, new[] { P(9.3f, 9.6f), P(11.3f, 11.6f), P(14.8f, 7.6f) });
                            g.DrawLines(pen, new[] { P(8, 15), P(6.5f, 21), P(12, 19), P(17.5f, 21), P(16, 15) });
                        }
                        break;
                    case "open":
                        g.DrawLines(pen, new[] { P(3, 19), P(3, 5), P(9, 5), P(11, 7.5f), P(19, 7.5f), P(19, 10) });
                        g.DrawPolygon(pen, new[] { P(3, 19), P(6.5f, 11), P(22, 11), P(18.5f, 19) });
                        break;
                    case "pdf":
                        // a sheet with a folded corner and a red "PDF" band across it
                        g.DrawLines(pen, new[] { P(14, 3), P(5, 3), P(5, 21), P(19, 21), P(19, 8), P(14, 3), P(14, 8), P(19, 8) });
                        using (var band = new SolidBrush(onDark ? col : Theme.Red)) g.FillRectangle(band, r.Left + 2 * u, r.Top + 11.5f * u, 15 * u, 6.5f * u);
                        using (var f = Fonts.Get("uiBold", 5.2f * u)) using (var white = new SolidBrush(onDark ? Theme.Green : Color.White))
                        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            g.DrawString("PDF", f, white, new RectangleF(r.Left + 2 * u, r.Top + 11.5f * u, 15 * u, 6.8f * u), sf);
                        break;
                    case "pptx":
                    case "addpptx":
                        {
                            // a slide on a stand with an orange "P" (PowerPoint's colour); the add version has a plus
                            bool add = kind == "addpptx";
                            g.DrawRectangle(pen, r.Left + 3 * u, r.Top + 4 * u, (add ? 14 : 18) * u, 12 * u);
                            g.DrawLine(pen, P(add ? 10 : 12, 16), P(add ? 10 : 12, 20));
                            g.DrawLine(pen, P(add ? 7 : 8, 20.5f), P(add ? 13 : 16, 20.5f));
                            using (var band = new SolidBrush(onDark ? col : Color.FromArgb(208, 74, 36))) g.FillRectangle(band, r.Left + 5.5f * u, r.Top + 6.5f * u, 6.5f * u, 7 * u);
                            using (var f = Fonts.Get("uiBold", 5.6f * u)) using (var white = new SolidBrush(onDark ? Theme.Green : Color.White))
                            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                                g.DrawString("P", f, white, new RectangleF(r.Left + 5.5f * u, r.Top + 6.6f * u, 6.5f * u, 7 * u), sf);
                            if (add)
                            {
                                g.DrawLine(pen, P(20, 8), P(20, 14));
                                g.DrawLine(pen, P(17, 11), P(23, 11));
                            }
                            break;
                        }
                    case "save":
                        g.DrawPolygon(pen, new[] { P(4, 4), P(16.5f, 4), P(20, 7.5f), P(20, 20), P(4, 20) });
                        g.DrawLines(pen, new[] { P(8, 4), P(8, 9), P(15, 9), P(15, 4) });
                        g.DrawRectangle(pen, r.Left + 8 * u, r.Top + 13 * u, 8 * u, 7 * u);
                        break;
                    case "image":
                        g.DrawRectangle(pen, r.Left + 3 * u, r.Top + 4 * u, 18 * u, 16 * u);
                        g.DrawLines(pen, new[] { P(3.5f, 17), P(9, 11.5f), P(13, 15.5f), P(15.5f, 13), P(20.5f, 18) });
                        g.FillEllipse(brush, r.Left + 14 * u, r.Top + 7 * u, 3.4f * u, 3.4f * u);
                        break;
                    case "rotl":
                    case "rotr":
                        {
                            bool right = kind == "rotr";
                            var ar = new RectangleF(r.Left + 4 * u, r.Top + 5 * u, 16 * u, 16 * u);
                            g.DrawArc(pen, ar, right ? 200 : -20, right ? 250 : -250);
                            var tip = right ? P(19.5f, 8) : P(4.5f, 8);
                            g.DrawLines(pen, right ? new[] { P(19.5f, 3.5f), tip, P(15, 8.5f) } : new[] { P(4.5f, 3.5f), tip, P(9, 8.5f) });
                            break;
                        }
                    case "addimage":
                        g.DrawLines(pen, new[] { P(13, 4), P(3, 4), P(3, 20), P(19, 20), P(19, 12) });
                        g.DrawLines(pen, new[] { P(3.5f, 17), P(8.5f, 12), P(12, 15.5f), P(14, 13.5f), P(18.5f, 18) });
                        g.DrawLine(pen, P(19, 2.5f), P(19, 9.5f));
                        g.DrawLine(pen, P(15.5f, 6), P(22.5f, 6));
                        break;
                    case "newlesson":
                        g.DrawLines(pen, new[] { P(13, 3), P(6, 3), P(6, 21), P(18, 21), P(18, 8), P(13, 3), P(13, 8), P(18, 8) });
                        g.DrawLine(pen, P(12, 11.5f), P(12, 17.5f));
                        g.DrawLine(pen, P(9, 14.5f), P(15, 14.5f));
                        break;
                    case "gear":
                        g.DrawEllipse(pen, r.Left + 8.5f * u, r.Top + 8.5f * u, 7 * u, 7 * u);
                        for (int a = 0; a < 8; a++)
                        {
                            double t = a * Math.PI / 4;
                            g.DrawLine(pen, cx + (float)Math.Cos(t) * 6.5f * u, cy + (float)Math.Sin(t) * 6.5f * u, cx + (float)Math.Cos(t) * 9.5f * u, cy + (float)Math.Sin(t) * 9.5f * u);
                        }
                        break;
                    case "keyboard":
                        g.DrawRectangle(pen, r.Left + 2.5f * u, r.Top + 6 * u, 19 * u, 12 * u);
                        foreach (var x in new[] { 6.5f, 10f, 13.5f, 17f }) g.FillEllipse(brush, r.Left + x * u - u, r.Top + 9 * u, 2 * u, 2 * u);
                        g.DrawLine(pen, P(8, 14.5f), P(16, 14.5f));
                        break;
                    case "info":
                        g.DrawEllipse(pen, r.Left + 3 * u, r.Top + 3 * u, 18 * u, 18 * u);
                        g.DrawLine(pen, P(12, 11), P(12, 16.5f));
                        g.FillEllipse(brush, cx - 1.3f * u, r.Top + 6.6f * u, 2.6f * u, 2.6f * u);
                        break;
                    case "prev": g.DrawLines(pen, new[] { P(15, 5), P(8, 12), P(15, 19) }); break;
                    case "next": g.DrawLines(pen, new[] { P(9, 5), P(16, 12), P(9, 19) }); break;
                    case "plus": g.DrawLine(pen, P(12, 5), P(12, 19)); g.DrawLine(pen, P(5, 12), P(19, 12)); break;
                    case "collapse": g.DrawLines(pen, new[] { P(6, 9), P(12, 15), P(18, 9) }); break;
                    case "expand": g.DrawLines(pen, new[] { P(6, 15), P(12, 9), P(18, 15) }); break;
                    case "exit":
                        using (var red = new Pen(onDark ? col : Theme.Red, 2.1f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        {
                            g.DrawLine(red, P(6, 6), P(18, 18));
                            g.DrawLine(red, P(18, 6), P(6, 18));
                        }
                        break;
                }
            }
            g.Restore(st);
        }
    }
}
