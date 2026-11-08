using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PhanTrang
{
    // Plain Windows data types, laid out exactly as user32/gdi32 expect them.

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
        public POINT(int x, int y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE
    {
        public int CX, CY;
        public SIZE(int cx, int cy) { CX = cx; CY = cy; }
    }

    /// <summary>A screen rectangle in physical pixels (right/bottom exclusive).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public RECT(int left, int top, int right, int bottom) { Left = left; Top = top; Right = right; Bottom = bottom; }
        public int Width { get { return Right - Left; } }
        public int Height { get { return Bottom - Top; } }
        public bool Contains(int x, int y) { return Left <= x && x < Right && Top <= y && y < Bottom; }
        public bool SameAs(RECT o) { return o.Left == Left && o.Top == Top && o.Right == Right && o.Bottom == Bottom; }
    }

    /// <summary>The monitor under a point: its full area, the part not covered by the taskbar, and its DPI scale.</summary>
    public struct ScreenInfo
    {
        public RECT Bounds, Work;
        public double Scale;
    }

    public delegate IntPtr LowLevelProc(int code, IntPtr wParam, IntPtr lParam);
    public delegate bool WindowVisitor(IntPtr hwnd, IntPtr data);

    /// <summary>Win32 calls used by Phấn Trắng, with their constants grouped by topic.</summary>
    public static class Win32
    {
        public static class Wm
        {
            public const int Activate = 0x21 /* WM_MOUSEACTIVATE */, SetCursor = 0x20, Quit = 0x12, HotKey = 0x312;
            public const int MouseMove = 0x200, LButtonDown = 0x201, LButtonUp = 0x202, RButtonDown = 0x204, RButtonUp = 0x205;
        }

        public static class Mod
        {
            public const uint Alt = 0x1, Ctrl = 0x2, Shift = 0x4, Win = 0x8, NoRepeat = 0x4000;
        }

        public static class Vk
        {
            public const int Shift = 0x10, Ctrl = 0x11, Alt = 0x12, Escape = 0x1B;
        }

        public static class Swp
        {
            public const uint NoSize = 0x1, NoMove = 0x2, NoActivate = 0x10, ShowWindow = 0x40;
            public const uint KeepPlace = NoSize | NoMove | NoActivate;
        }

        public static class Ws
        {
            public const int Popup = unchecked((int)0x80000000);
            public const int ExTransparent = 0x20, ExToolWindow = 0x80, ExTopmost = 0x8, ExLayered = 0x80000, ExNoActivate = 0x8000000;
        }

        public const int MouseActivateNo = 3;          // MA_NOACTIVATE
        public const int ShowHide = 0, ShowNoActivate = 4;
        public const uint LayeredAlpha = 2;            // ULW_ALPHA
        public const uint SrcCopy = 0x00CC0020;
        public static readonly IntPtr Topmost = new IntPtr(-1), NotTopmost = new IntPtr(-2);

        // ---- windows ----
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int how);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool EnumWindows(WindowVisitor visit, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder name, int capacity);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int pid);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

        // ---- layered drawing ----
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr screenDc, ref POINT dst, ref SIZE size,
            IntPtr srcDc, ref POINT src, uint colorKey, ref BLENDFUNCTION blend, uint flags);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern unsafe bool UpdateLayeredWindowIndirect(IntPtr hwnd, UPDATELAYEREDWINDOWINFO* info);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool GdiFlush();
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFOHEADER header, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint op);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

        // ---- input ----
        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int kind, LowLevelProc proc, IntPtr module, uint thread);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern int GetMessage(out MSG msg, IntPtr hwnd, uint first, uint last);
        [DllImport("user32.dll")] public static extern bool PostThreadMessage(uint thread, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("kernel32.dll")] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();

        public static bool KeyDown(int vk) { return vk != 0 && (GetAsyncKeyState(vk) & 0x8000) != 0; }
        public static POINT CursorPos() { POINT p; GetCursorPos(out p); return p; }

        // ---- cursors ----
        [DllImport("user32.dll")] public static extern IntPtr CreateCursor(IntPtr instance, int hotX, int hotY, int w, int h, byte[] andMask, byte[] xorMask);
        [DllImport("user32.dll")] public static extern IntPtr CopyIcon(IntPtr icon);
        [DllImport("user32.dll")] public static extern bool SetSystemCursor(IntPtr cursor, uint id);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint action, uint param, IntPtr data, uint flags);

        // ---- timing & DPI ----
        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint ms);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr context);

        // ---- monitors ----
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT p, uint flags);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int kind, out uint x, out uint y);

        /// <summary>The monitor at (x, y), or the nearest one.</summary>
        public static ScreenInfo ScreenAt(int x, int y)
        {
            IntPtr handle = MonitorFromPoint(new POINT(x, y), 2 /* nearest */);
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            GetMonitorInfo(handle, ref mi);
            var s = new ScreenInfo { Bounds = mi.rcMonitor, Work = mi.rcWork, Scale = 1 };
            try
            {
                uint dx, dy;
                if (GetDpiForMonitor(handle, 0, out dx, out dy) == 0 && dx > 0) s.Scale = dx / 96.0;
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            return s;
        }

        /// <summary>The monitor the mouse is on.</summary>
        public static ScreenInfo ScreenAtCursor() { var p = CursorPos(); return ScreenAt(p.X, p.Y); }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct UPDATELAYEREDWINDOWINFO
    {
        public int cbSize;
        public IntPtr hdcDst;
        public POINT* pptDst;
        public SIZE* psize;
        public IntPtr hdcSrc;
        public POINT* pptSrc;
        public uint crKey;
        public BLENDFUNCTION* pblend;
        public uint dwFlags;
        public RECT* prcDirty;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight;
        public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public POINT pt; }
}
