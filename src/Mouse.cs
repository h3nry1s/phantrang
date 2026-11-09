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
        /// <summary>More holes of any shape (the geometry instruments). Called on the hook thread: must be quick and thread-safe.</summary>
        public volatile Func<int, int, bool> HoleTest;
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
        /// <summary>For the self-tests: replaces the real keyboard when asking whether the bypass key is held.</summary>
        public Func<bool> BypassProbe;
        public bool BypassHeld { get { var probe = BypassProbe; return probe != null ? probe() : Win32.KeyDown(BypassKey); } }

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
            if (t != null)
            {
                worker = null;
                Win32.PostThreadMessage(workerId, Win32.Wm.Quit, IntPtr.Zero, IntPtr.Zero);
                t.Join(1500);
            }
            LetGo();
        }

        /// <summary>
        /// A press we kept whose release we will now never see (the hook is going away): report the release
        /// ourselves, so the app never keeps drawing after the button is up.
        /// </summary>
        void LetGo()
        {
            for (int b = 0; b < 2; b++)
            {
                int was = owner[b];
                owner[b] = 0;
                if (was == 1) Send(GateKind.Up, b, Win32.CursorPos());
                else if (was == 2) Send(GateKind.BypassUp, b, Win32.CursorPos());
            }
        }

        /// <summary>Windows silently removes a low-level hook that answered too slowly once; this puts it back.</summary>
        public bool Restart() { Stop(); return Start(); }

        bool InHole(POINT p)
        {
            foreach (var r in Holes) if (r.Contains(p.X, p.Y)) return true;
            var test = HoleTest;
            if (test != null && test(p.X, p.Y)) return true;
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
                if (Decide(wParam.ToInt32(), (data.flags & LLMHF_INJECTED) != 0, data.pt)) return new IntPtr(1);
            }
            return Win32.CallNextHookEx(handle, code, wParam, lParam);
        }

        /// <summary>
        /// What to do with one mouse message (msg = WM_xxx): reports it if it is ours, and returns true when it must
        /// not reach the program underneath. A press we keep always has its release kept and reported too, wherever
        /// the mouse is by then. Public for the self-tests.
        /// </summary>
        public bool Decide(int msg, bool injected, POINT pt)
        {
            int button = msg == Win32.Wm.LButtonDown || msg == Win32.Wm.LButtonUp ? 0
                       : msg == Win32.Wm.RButtonDown || msg == Win32.Wm.RButtonUp ? 1 : -1;
            bool down = msg == Win32.Wm.LButtonDown || msg == Win32.Wm.RButtonDown;

            if (msg == Win32.Wm.MouseMove)
            {
                if (Capturing) Send(GateKind.Move, -1, pt);
            }
            else if (button >= 0 && down)
            {
                if (Capturing && !injected)
                {
                    bool bypass = BypassHeld;
                    if (!bypass && !InHole(pt))
                    {
                        owner[button] = 1;
                        Send(GateKind.Down, button, pt);
                        return true;
                    }
                    owner[button] = 2;
                    if (bypass) Send(GateKind.BypassDown, button, pt);
                }
            }
            else if (button >= 0)
            {
                int was = owner[button];
                owner[button] = 0;
                if (was == 1) { Send(GateKind.Up, button, pt); return true; }
                if (was == 2) Send(GateKind.BypassUp, button, pt);
            }
            return false;
        }
    }

    /// <summary>
    /// A safety net for "the button is up but Phấn Trắng still thinks it is held". Windows may skip a low-level
    /// hook once (it answered too slowly) or remove it; the release is then lost and the line would follow the
    /// mouse. Raw input reports the buttons independently of hooks: once it says the button went up after this
    /// press began, and the gate still has not reported it a moment later, the press is ended.
    /// Times are in seconds (<see cref="Clock.Now"/>); button 0 = left, 1 = right.
    /// </summary>
    public sealed class HoldGuard
    {
        /// <summary>How long the gate gets to report the release itself before the guard steps in.</summary>
        public const double Grace = 0.25;
        /// <summary>A raw press this much before the gate's press still counts as the same press.</summary>
        const double Slack = 0.5;

        readonly double[] pressed = { -1, -1 }, rawDown = { -1, -1 }, rawUp = { -1, -1 };

        public void Pressed(int b, double t) { if (b == 0 || b == 1) pressed[b] = t; }
        public void Released(int b) { if (b == 0 || b == 1) pressed[b] = -1; }
        public void RawDown(int b, double t) { if (b == 0 || b == 1) rawDown[b] = t; }
        public void RawUp(int b, double t) { if (b == 0 || b == 1) rawUp[b] = t; }

        /// <summary>True when button b is still held for the app but the mouse let go of it a while ago.</summary>
        public bool Stuck(int b, double now)
        {
            if (b < 0 || b > 1 || pressed[b] < 0) return false;
            if (rawDown[b] < 0 || rawDown[b] < pressed[b] - Slack) return false;   // raw input never saw this press (pen, touch): no opinion
            if (rawUp[b] < rawDown[b]) return false;                               // still down
            return now - rawUp[b] >= Grace;
        }
    }

    /// <summary>Listens to raw mouse input (even while other programs are in front) and reports button presses and releases.</summary>
    public sealed class RawMouse : NativeWindow
    {
        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTDEVICE { public ushort UsagePage, Usage; public uint Flags; public IntPtr Target; }

        [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);
        [DllImport("user32.dll")] static extern uint GetRawInputData(IntPtr raw, uint command, IntPtr data, ref uint size, uint headerSize);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);

        const int WmInput = 0x00FF;
        const uint RidInput = 0x10000003, RideInputSink = 0x100, RimTypeMouse = 0;
        const ushort LeftDown = 0x1, LeftUp = 0x2, RightDown = 0x4, RightUp = 0x8;

        /// <summary>(button, down): button 0 = left, 1 = right, as the app sees them (swapped buttons taken into account).</summary>
        public event Action<int, bool> Button;
        public bool Working { get; private set; }

        public RawMouse() { CreateHandle(new CreateParams()); }

        /// <summary>Starts listening (only while Phấn Trắng is on: every mouse move sends a message).</summary>
        public void Start()
        {
            if (Working) return;
            var device = new[] { new RAWINPUTDEVICE { UsagePage = 1, Usage = 2, Flags = RideInputSink, Target = Handle } };
            Working = RegisterRawInputDevices(device, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
            if (!Working) Log.Write("không đăng ký được raw input cho chuột: " + Marshal.GetLastWin32Error());
        }

        public void Stop()
        {
            if (!Working) return;
            var device = new[] { new RAWINPUTDEVICE { UsagePage = 1, Usage = 2, Flags = 0x1 /* RIDEV_REMOVE */, Target = IntPtr.Zero } };
            RegisterRawInputDevices(device, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
            Working = false;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmInput) Read(m.LParam);
            base.WndProc(ref m);   // DefWindowProc frees the raw input
        }

        void Read(IntPtr raw)
        {
            uint headerSize = (uint)(8 + 2 * IntPtr.Size), size = 0;
            GetRawInputData(raw, RidInput, IntPtr.Zero, ref size, headerSize);
            if (size == 0 || size > 1024) return;
            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(raw, RidInput, buffer, ref size, headerSize) != size) return;
                if ((uint)Marshal.ReadInt32(buffer) != RimTypeMouse) return;
                // RAWMOUSE: usFlags (2 bytes), then, aligned to 4, usButtonFlags
                ushort flags = (ushort)Marshal.ReadInt16(buffer, (int)headerSize + 4);
                bool swapped = GetSystemMetrics(23 /* SM_SWAPBUTTON */) != 0;
                int left = swapped ? 1 : 0, right = 1 - left;
                var report = Button;
                if (report == null) return;
                if ((flags & LeftDown) != 0) report(left, true);
                if ((flags & LeftUp) != 0) report(left, false);
                if ((flags & RightDown) != 0) report(right, true);
                if ((flags & RightUp) != 0) report(right, false);
            }
            finally { Marshal.FreeHGlobal(buffer); }
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
