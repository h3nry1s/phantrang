using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace PhanTrang
{
    /// <summary>Shared drawing: rounded rectangles and the Phấn Trắng mark.</summary>
    static class Draw
    {
        /// <summary>A rectangle with all four corners rounded by 'radius' (clamped to fit).</summary>
        public static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = Math.Max(1, Math.Min(2 * radius, Math.Min(r.Width, r.Height)));
            var path = new GraphicsPath();
            // corners clockwise from top-left; each quarter arc starts where the previous one ended
            var corners = new[] { new PointF(r.Left, r.Top), new PointF(r.Right - d, r.Top), new PointF(r.Right - d, r.Bottom - d), new PointF(r.Left, r.Bottom - d) };
            for (int i = 0; i < 4; i++) path.AddArc(corners[i].X, corners[i].Y, d, d, 180 + 90 * i, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>The Phấn Trắng mark: a stick of chalk with a chalk stroke, on a green board.</summary>
        public static void Logo(Graphics g, RectangleF r)
        {
            var saved = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(r.X, r.Y);
            g.ScaleTransform(r.Width / 96f, r.Height / 96f);   // drawn on a 96 x 96 grid
            using (var board = Round(new RectangleF(4, 4, 88, 88), 26))
            using (var green = new SolidBrush(Theme.Green)) g.FillPath(green, board);
            using (var stroke = Geo.RoundPen(Color.FromArgb(190, Theme.Chalk), 4)) g.DrawBezier(stroke, 18, 74, 30, 64, 42, 82, 54, 72);
            g.TranslateTransform(50, 44);
            g.RotateTransform(-35);
            using (var stick = Round(new RectangleF(-28, -10, 56, 20), 6))
            using (var chalk = new SolidBrush(Theme.Chalk)) g.FillPath(chalk, stick);
            using (var shine = new SolidBrush(Color.White)) g.FillRectangle(shine, -24, -10, 50, 6);
            using (var worn = new SolidBrush(Color.FromArgb(226, 218, 202))) g.FillRectangle(worn, -28, -9, 11, 18);
            g.Restore(saved);
        }
    }
}
