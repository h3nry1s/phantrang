using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace PhanTrang
{
    /// <summary>Monotonic time in seconds, used for every animation.</summary>
    public static class Clock
    {
        static readonly double tick = 1.0 / Stopwatch.Frequency;
        public static double Now { get { return Stopwatch.GetTimestamp() * tick; } }
    }

    /// <summary>
    /// A borderless, always-on-top window whose pixels come from a <see cref="PixelBuffer"/> with per-pixel alpha.
    /// It never takes focus. When see-through, clicks fall to whatever is underneath.
    /// </summary>
    public sealed class GlassWindow : NativeWindow
    {
        /// <summary>Messages other than the activation ones, for windows that react to the mouse.</summary>
        public event Action<Message> Received;

        public GlassWindow(bool seeThrough)
        {
            int ex = Win32.Ws.ExLayered | Win32.Ws.ExTopmost | Win32.Ws.ExToolWindow | Win32.Ws.ExNoActivate;
            if (seeThrough) ex |= Win32.Ws.ExTransparent;
            CreateHandle(new CreateParams { Caption = "PhanTrang", Style = Win32.Ws.Popup, ExStyle = ex, Width = 1, Height = 1 });
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Win32.Wm.Activate)
            {
                m.Result = new IntPtr(Win32.MouseActivateNo);
                return;
            }
            var r = Received;
            if (r != null) r(m);
            base.WndProc(ref m);
        }

        public void Show()
        {
            Win32.ShowWindow(Handle, Win32.ShowNoActivate);
            Raise();
        }

        public void Hide() { Win32.ShowWindow(Handle, Win32.ShowHide); }

        /// <summary>Back on top of other topmost windows (a slide show may have pushed in front).</summary>
        public void Raise() { Win32.SetWindowPos(Handle, Win32.Topmost, 0, 0, 0, 0, Win32.Swp.KeepPlace); }
    }

    /// <summary>
    /// A top-down 32-bit premultiplied DIB section: GDI+ draws into <see cref="G"/>, plain GDI can blit from
    /// <see cref="Hdc"/>, and <see cref="Show"/> hands the pixels to a layered window.
    /// </summary>
    public sealed class PixelBuffer : IDisposable
    {
        public readonly int W, H;
        public readonly Bitmap Bmp;
        public readonly Graphics G;
        readonly IntPtr dc, section, previous;

        public IntPtr Hdc { get { return dc; } }

        public PixelBuffer(int width, int height)
        {
            W = Math.Max(1, width);
            H = Math.Max(1, height);
            var header = new BITMAPINFOHEADER { biSize = 40, biWidth = W, biHeight = -H, biPlanes = 1, biBitCount = 32 };
            IntPtr bits;
            dc = Win32.CreateCompatibleDC(IntPtr.Zero);
            section = Win32.CreateDIBSection(dc, ref header, 0, out bits, IntPtr.Zero, 0);
            previous = Win32.SelectObject(dc, section);
            Bmp = new Bitmap(W, H, W * 4, PixelFormat.Format32bppPArgb, bits);
            G = Graphics.FromImage(Bmp);
        }

        /// <summary>Puts the buffer on screen at (x, y). With a dirty rectangle only that part is sent.</summary>
        public unsafe void Show(IntPtr hwnd, int x, int y, Rectangle? dirty = null)
        {
            G.Flush(FlushIntention.Sync);
            Win32.GdiFlush();
            var at = new POINT(x, y);
            var size = new SIZE(W, H);
            var origin = new POINT(0, 0);
            var blend = new BLENDFUNCTION { SourceConstantAlpha = 255, AlphaFormat = 1 /* AC_SRC_ALPHA */ };

            if (dirty.HasValue)
            {
                var part = Rectangle.Intersect(dirty.Value, new Rectangle(0, 0, W, H));
                if (part.Width <= 0 || part.Height <= 0) return;
                var rc = new RECT(part.Left, part.Top, part.Right, part.Bottom);
                var info = new UPDATELAYEREDWINDOWINFO
                {
                    cbSize = sizeof(UPDATELAYEREDWINDOWINFO),
                    pptDst = &at, psize = &size, hdcSrc = dc, pptSrc = &origin,
                    pblend = &blend, dwFlags = Win32.LayeredAlpha, prcDirty = &rc,
                };
                if (Win32.UpdateLayeredWindowIndirect(hwnd, &info)) return;
                // some drivers refuse partial updates: fall back to sending everything
            }
            Win32.UpdateLayeredWindow(hwnd, IntPtr.Zero, ref at, ref size, dc, ref origin, 0, ref blend, Win32.LayeredAlpha);
        }

        public void Dispose()
        {
            G.Dispose();
            Bmp.Dispose();
            Win32.SelectObject(dc, previous);
            Win32.DeleteObject(section);
            Win32.DeleteDC(dc);
        }
    }
}
