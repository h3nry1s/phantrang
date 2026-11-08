using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace PhanTrang
{
    /// <summary>A round "Bật Phấn Trắng" button that floats in the corner while Phấn Trắng is off.
    /// Click to turn on; drag to move it (the spot is remembered for this session).</summary>
    class QuickButton
    {
        readonly GlassWindow win = new GlassWindow(false);
        PixelBuffer surf;
        int x = int.MinValue, y;
        double sc = 1;
        bool hover, dragging, moved, shown;
        Point grab;
        public event Action Clicked;

        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SetCapture(IntPtr h);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT e);
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct TRACKMOUSEEVENT { public int cbSize; public uint dwFlags; public IntPtr hwnd; public uint time; }

        const int WM_MOUSELEAVE = 0x2A3;

        public QuickButton()
        {
            win.Received += m =>
            {
                int mx = (short)(m.LParam.ToInt64() & 0xFFFF), my = (short)((m.LParam.ToInt64() >> 16) & 0xFFFF);
                switch (m.Msg)
                {
                    case Win32.Wm.LButtonDown:
                        dragging = true; moved = false; grab = new Point(mx, my); SetCapture(win.Handle);
                        break;
                    case Win32.Wm.MouseMove:
                        if (!hover)
                        {
                            hover = true; Paint();
                            var t = new TRACKMOUSEEVENT { cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(TRACKMOUSEEVENT)), dwFlags = 2, hwnd = win.Handle };
                            TrackMouseEvent(ref t);
                        }
                        if (dragging && (Math.Abs(mx - grab.X) > 4 || Math.Abs(my - grab.Y) > 4 || moved))
                        {
                            moved = true;
                            var p = Win32.CursorPos();
                            x = p.X - grab.X; y = p.Y - grab.Y;
                            Paint();
                        }
                        break;
                    case Win32.Wm.LButtonUp:
                        ReleaseCapture();
                        bool click = dragging && !moved;
                        dragging = false;
                        if (click && Clicked != null) Clicked();
                        break;
                    case WM_MOUSELEAVE:
                        hover = false; Paint();
                        break;
                }
            };
        }

        int Size { get { return (int)Math.Round(64 * sc); } }

        public void Show()
        {
            var screen = Win32.ScreenAtCursor();
            RECT work = screen.Work;
            double scale = screen.Scale;
            if (x == int.MinValue || Math.Abs(scale - sc) > 0.01)
            {
                sc = scale;
                x = work.Right - Size - (int)(24 * sc);
                y = work.Bottom - Size - (int)(24 * sc);
            }
            shown = true;
            Paint();
            win.Show();
        }

        public void Hide() { shown = false; win.Hide(); }

        void Paint()
        {
            if (!shown) return;
            int pad = (int)(8 * sc), S = Size + pad * 2;
            if (surf == null || surf.W != S)
            {
                if (surf != null) surf.Dispose();
                surf = new PixelBuffer(S, S);
            }
            var g = surf.G;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.Clear(Color.Transparent);
            g.CompositingMode = CompositingMode.SourceOver;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            var r = new RectangleF(pad, pad, Size, Size);
            using (var sh = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(sh, r.X, r.Y + 3 * (float)sc, r.Width, r.Height);
            using (var b = new SolidBrush(hover ? Color.FromArgb(38, 92, 73) : Theme.Green)) g.FillEllipse(b, r);
            using (var ring = new Pen(Theme.Chalk, (float)Math.Round(2 * sc))) g.DrawEllipse(ring, RectangleF.Inflate(r, -(float)Math.Round(3 * sc), -(float)Math.Round(3 * sc)));
            // the chalk mark, without its square background
            var st = g.Save();
            g.TranslateTransform(r.X + r.Width / 2, r.Y + r.Height / 2);
            g.RotateTransform(-35);
            float u = Size / 96f;
            using (var p = Draw.Round(new RectangleF(-26 * u, -9 * u, 52 * u, 18 * u), 6 * u)) using (var b = new SolidBrush(Theme.Chalk)) g.FillPath(b, p);
            using (var b = new SolidBrush(Color.White)) g.FillRectangle(b, -22 * u, -9 * u, 46 * u, 5 * u);
            using (var b = new SolidBrush(Color.FromArgb(226, 218, 202))) g.FillRectangle(b, -26 * u, -8 * u, 10 * u, 16 * u);
            g.Restore(st);
            surf.Show(win.Handle, x - pad, y - pad);
        }

        public void Raise() { if (shown) win.Raise(); }
    }
}
