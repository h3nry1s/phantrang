using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace PhanTrang
{
    public enum GateKind { Move, Down, Up, BypassDown, BypassUp }

    /// <summary>One mouse event as seen by the gate. Button: 0 = left, 1 = right.</summary>
    public struct GateEvent
    {
        public GateKind Kind;
        public int Button;
        public POINT At;
    }

    /// <summary>
    /// Decides, for every mouse click on the whole desktop, whether it belongs to Phấn Trắng (kept, and reported as
    /// Down/Up) or to the program underneath (let through). Clicks are let through over "holes" (our own windows,
    /// the taskbar) and while the bypass key is held. Runs a WH_MOUSE_LL hook on a dedicated thread so a busy UI
    /// never makes Windows drop the hook; <see cref="Report"/> is called on that thread and must only queue work.
    /// </summary>
    public sealed class ClickGate
    {
        const int WH_MOUSE_LL = 14;
        const uint LLMHF_INJECTED = 1;

        public volatile bool Capturing;
        public volatile RECT[] Holes = new RECT[0];
        public volatile int BypassKey = Win32.Vk.Alt;
        public Action<GateEvent> Report;

        readonly LowLevelProc callback;     // kept in a field: the delegate must outlive the hook
        Thread worker;
        uint workerId;
        IntPtr handle;
        long lastCallTicks;
        // per button: 1 = we kept the press (so we keep its release), 2 = it went through while bypassing
        readonly int[] owner = new int[2];

        public ClickGate() { callback = OnHook; }

        public bool Running { get { return handle != IntPtr.Zero; } }
        public bool BypassHeld { get { return Win32.KeyDown(BypassKey); } }

        /// <summary>Seconds since Windows last called the hook (mouse moves call it constantly).</summary>
        public double SilentFor { get { return (DateTime.UtcNow.Ticks - Interlocked.Read(ref lastCallTicks)) / (double)TimeSpan.TicksPerSecond; } }

        public bool Start()
        {
            if (worker != null) return Running;
            Interlocked.Exchange(ref lastCallTicks, DateTime.UtcNow.Ticks);
            using (var started = new ManualResetEvent(false))
            {
                worker = new Thread(() => Loop(started)) { IsBackground = true, Priority = ThreadPriority.Highest, Name = "PhanTrang click gate" };
                worker.Start();
                started.WaitOne(3000);
            }
            return Running;
        }

        void Loop(ManualResetEvent started)
        {
            workerId = Win32.GetCurrentThreadId();
            handle = Win32.SetWindowsHookEx(WH_MOUSE_LL, callback, Win32.GetModuleHandle(null), 0);
            try { started.Set(); } catch (ObjectDisposedException) { }
            MSG m;
            while (Win32.GetMessage(out m, IntPtr.Zero, 0, 0) > 0) { }
            if (handle != IntPtr.Zero) Win32.UnhookWindowsHookEx(handle);
            handle = IntPtr.Zero;
        }

        public void Stop()
        {
            var t = worker;
            if (t == null) return;
            worker = null;
            Win32.PostThreadMessage(workerId, Win32.Wm.Quit, IntPtr.Zero, IntPtr.Zero);
            t.Join(1500);
            owner[0] = owner[1] = 0;
        }

        /// <summary>Windows silently removes a low-level hook that answered too slowly once; this puts it back.</summary>
        public bool Restart() { Stop(); return Start(); }

        bool InHole(POINT p)
        {
            foreach (var r in Holes) if (r.Contains(p.X, p.Y)) return true;
            return false;
        }

        void Send(GateKind kind, int button, POINT at)
        {
            var r = Report;
            if (r == null) return;
            try { r(new GateEvent { Kind = kind, Button = button, At = at }); }
            catch (Exception) { }   // the UI may be shutting down; never let that take the hook thread with it
        }

        IntPtr OnHook(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                Interlocked.Exchange(ref lastCallTicks, DateTime.UtcNow.Ticks);
                var data = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                int msg = wParam.ToInt32();
                int button = msg == Win32.Wm.LButtonDown || msg == Win32.Wm.LButtonUp ? 0
                           : msg == Win32.Wm.RButtonDown || msg == Win32.Wm.RButtonUp ? 1 : -1;
                bool down = msg == Win32.Wm.LButtonDown || msg == Win32.Wm.RButtonDown;

                if (msg == Win32.Wm.MouseMove)
                {
                    if (Capturing) Send(GateKind.Move, -1, data.pt);
                }
                else if (button >= 0 && down)
                {
                    bool synthetic = (data.flags & LLMHF_INJECTED) != 0;
                    if (Capturing && !synthetic)
                    {
                        bool bypass = BypassHeld;
                        if (!bypass && !InHole(data.pt))
                        {
                            owner[button] = 1;
                            Send(GateKind.Down, button, data.pt);
                            return new IntPtr(1);
                        }
                        owner[button] = 2;
                        if (bypass) Send(GateKind.BypassDown, button, data.pt);
                    }
                }
                else if (button >= 0)
                {
                    int was = owner[button];
                    owner[button] = 0;
                    if (was == 1) { Send(GateKind.Up, button, data.pt); return new IntPtr(1); }
                    if (was == 2) Send(GateKind.BypassUp, button, data.pt);
                }
            }
            return Win32.CallNextHookEx(handle, code, wParam, lParam);
        }
    }

    /// <summary>
    /// While Phấn Trắng draws its own pointer, every system cursor shape is swapped for an empty one.
    /// Reloading the user's cursor scheme (SPI_SETCURSORS) is always safe, so it is also done at start-up
    /// in case an earlier run was killed with the cursor still blank.
    /// </summary>
    public static class BlankCursor
    {
        static readonly uint[] Shapes =
        {
            32512, 32513, 32514, 32515, 32516, 32642, 32643, 32644,
            32645, 32646, 32648, 32649, 32650, 32651, 32671, 32672,
        };
        static IntPtr empty;

        public static bool On { get; private set; }

        public static void Apply()
        {
            if (On) return;
            if (empty == IntPtr.Zero)
            {
                var andMask = new byte[128];
                for (int i = 0; i < andMask.Length; i++) andMask[i] = 0xFF;   // AND all-ones + XOR zero = see-through
                empty = Win32.CreateCursor(IntPtr.Zero, 0, 0, 32, 32, andMask, new byte[128]);
            }
            for (int i = 0; i < Shapes.Length; i++) Win32.SetSystemCursor(Win32.CopyIcon(empty), Shapes[i]);   // the copy is owned by Windows
            On = true;
        }

        public static void Lift() { if (On) Reset(); }

        public static void Reset()
        {
            Win32.SystemParametersInfo(0x57 /* SPI_SETCURSORS */, 0, IntPtr.Zero, 0);
            On = false;
        }
    }

    /// <summary>System-wide shortcuts (RegisterHotKey), delivered to a hidden message-only window.</summary>
    public sealed class HotKeyWindow : NativeWindow
    {
        public event Action<int> Fired;

        public HotKeyWindow() { CreateHandle(new CreateParams()); }

        public bool Bind(int id, uint mods, Keys key)
        {
            Win32.UnregisterHotKey(Handle, id);
            return Win32.RegisterHotKey(Handle, id, mods | Win32.Mod.NoRepeat, (uint)key);
        }

        public void Unbind(int id) { Win32.UnregisterHotKey(Handle, id); }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Win32.Wm.HotKey)
            {
                var f = Fired;
                if (f != null) f(m.WParam.ToInt32());
            }
            base.WndProc(ref m);
        }
    }
}
