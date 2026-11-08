using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PhanTrang
{
    /// <summary>"Gõ lên bảng": type with the keyboard, the line appears on the board in a handwriting font.
    /// Enter = next line. Maths signs come from the symbol row or from short cuts (^2 → ², >= → ≥, sqrt → √).
    /// Floats above the toolbar while the Gõ chữ tool is on.</summary>
    class TypePanel : Form
    {
        public const int PanelW = 1060;
        readonly TextBox box = new TextBox();
        RoundButton newLine, removeLast, close;
        readonly ChipRow chips;
        readonly SymbolRow symbols;
        readonly float k;
        bool replacing;
        public event Action<string> TextTyped;
        public event Action Commit, RemoveLast, CloseRequested, LookChanged;

        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr pid);
        [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);

        int S(double v) { return (int)Math.Round(v * k); }

        public TypePanel(double scale)
        {
            k = (float)scale;
            // a borderless floating strip that stays above the slide and the board, not in the taskbar
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            BackColor = Theme.Paper;
            Font = Ui.Body(k);
            ClientSize = new Size(S(PanelW), S(190));

            // the buttons sit at the right end of the first row; the text field fills the space before them
            int buttonsLeft = PanelW - 14 - 124 - 10 - 116 - 10 - 68;
            var host = new InputHost(k) { Location = new Point(S(176), S(14)), Size = new Size(S(buttonsLeft - 12 - 176), S(44)) };
            box.BorderStyle = BorderStyle.None;
            box.BackColor = Color.White;
            box.Font = Fonts.Get("ui", 18 * k);
            box.AutoSize = false;   // the automatic height cut off the tails of g, p, q, y
            box.SetBounds(S(14), (host.Height - S(30)) / 2, host.Width - S(28), S(30));
            int btnX = buttonsLeft;
            box.KeyDown += (s, e) =>
            {
                Action raise = e.KeyCode == Keys.Enter ? Commit : e.KeyCode == Keys.Escape ? CloseRequested : null;
                if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Escape) return;
                e.SuppressKeyPress = true;
                if (raise != null) raise();
            };
            box.TextChanged += (s, e) =>
            {
                if (!replacing) AutoReplace();
                if (TextTyped != null) TextTyped(box.Text);
            };
            box.HandleCreated += (s, e) => SendMessage(box.Handle, 0x1501 /* EM_SETCUEBANNER */, (IntPtr)1, "Gõ chữ rồi nhấn Enter.  Gõ tắt: ^2 thành ²,  >= thành ≥,  sqrt thành √");
            host.Controls.Add(box);
            host.Click += (s, e) => box.Focus();
            Controls.Add(host);

            newLine = Place(new RoundButton("Xuống dòng ↵", RoundButton.Kind.Primary, k), S(btnX), S(124));
            removeLast = Place(new RoundButton("Xoá dòng cuối", RoundButton.Kind.Secondary, k), S(btnX + 134), S(116));
            close = Place(new RoundButton("Đóng", RoundButton.Kind.Secondary, k), S(btnX + 260), S(68));
            newLine.Click += (s, e) => { if (Commit != null) Commit(); box.Focus(); };
            removeLast.Click += (s, e) => { if (RemoveLast != null) RemoveLast(); box.Focus(); };
            close.Click += (s, e) => { if (CloseRequested != null) CloseRequested(); };

            chips = new ChipRow(k) { Location = new Point(S(14), S(68)), Size = new Size(S(PanelW - 28), S(60)) };
            chips.Changed += () => { if (LookChanged != null) LookChanged(); box.Focus(); };
            Controls.Add(chips);

            symbols = new SymbolRow(k) { Location = new Point(S(14), S(138)), Size = new Size(S(PanelW - 28), S(38)) };
            symbols.Picked += sym => { box.SelectedText = sym; box.Focus(); };
            Controls.Add(symbols);

            HandleCreated += (s, e) => Ui.RoundWindow(Handle);
            Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Theme.Line, 1)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                ToolIcons.Draw(g, "type", new Rectangle(S(16), S(18), S(24), S(24)), Theme.Green, false);
                using (var f = Ui.ItemTitle(k)) Ui.Text(g, "Gõ lên bảng", f, Theme.Green, new Rectangle(S(48), S(13), S(128), S(22)), TextFormatFlags.Left);
                using (var f = Ui.Caption(k)) Ui.Text(g, "Enter: xuống dòng", f, Theme.Dim, new Rectangle(S(48), S(35), S(128), S(20)), TextFormatFlags.Left);
            };
            ResumeLayout(false);
        }

        void AutoReplace()
        {
            string text; int caret;
            if (!MathInput.Apply(box.Text, box.SelectionStart, out text, out caret)) return;
            replacing = true;
            box.Text = text;
            box.SelectionStart = caret;
            replacing = false;
        }

        RoundButton Place(RoundButton b, int x, int w)
        {
            b.Location = new Point(x, S(14));
            b.Size = new Size(w, Math.Max(box.Height, S(44)));
            b.BackColor = Theme.Paper;
            Controls.Add(b);
            return b;
        }

        public string Typed { get { return box.Text; } }
        public void ClearText() { box.Text = ""; }

        /// <summary>Show above the toolbar and take the keyboard from the presentation app.</summary>
        public void Open(RECT work, int toolbarTop)
        {
            int x = work.Left + Math.Max(0, (work.Width - Width) / 2);
            int y = Math.Max(work.Top + S(8), toolbarTop - Height - S(10));
            Location = new Point(x, y);
            if (!Visible) Show();
            TakeFocus();
        }

        /// <summary>
        /// Windows only lets the program the user is working in (the slide show) give the keyboard away. Joining its
        /// input queue for a moment makes this panel count as part of it, so it may come to the front.
        /// </summary>
        public void TakeFocus()
        {
            uint mine = Win32.GetCurrentThreadId();
            IntPtr front = GetForegroundWindow();
            uint theirs = front == IntPtr.Zero ? 0 : GetWindowThreadProcessId(front, IntPtr.Zero);
            bool joined = theirs != 0 && theirs != mine && AttachThreadInput(mine, theirs, true);
            try
            {
                SetForegroundWindow(Handle);
                BringWindowToTop(Handle);
            }
            finally
            {
                if (joined) AttachThreadInput(mine, theirs, false);
            }
            Activate();
            box.Focus();
            box.Select(box.TextLength, 0);
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        public void Refresh2() { chips.Invalidate(); }
    }

    /// <summary>White rounded field around the text box (the box itself has no border).</summary>
    class InputHost : Panel
    {
        readonly float k;
        public InputHost(float k) { this.k = k; DoubleBuffered = true; BackColor = Theme.Paper; Cursor = Cursors.IBeam; }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float line = Math.Max(1, (int)Math.Round(2 * k));
            using (var field = Ui.Box(new RectangleF(0, 0, Width, Height), Ui.RadiusSmall * k, true))
            using (var white = new SolidBrush(Color.White))
            using (var green = new Pen(Theme.Green, line))
            {
                e.Graphics.FillPath(white, field);
                e.Graphics.DrawPath(green, field);
            }
        }
    }

    /// <summary>One click puts a maths sign at the caret. Hover shows its name and the typed short cut.</summary>
    class SymbolRow : Control
    {
        static readonly string[][] Signs =
        {
            new[] { "²", "bình phương (gõ ^2)" }, new[] { "³", "lập phương (gõ ^3)" }, new[] { "√", "căn bậc hai (gõ sqrt)" },
            new[] { "π", "số pi" }, new[] { "±", "cộng trừ (gõ +-)" }, new[] { "×", "nhân (gõ *)" }, new[] { "÷", "chia" },
            new[] { "≠", "khác (gõ !=)" }, new[] { "≈", "xấp xỉ (gõ ~~)" }, new[] { "≤", "bé hơn hoặc bằng (gõ <=)" },
            new[] { "≥", "lớn hơn hoặc bằng (gõ >=)" }, new[] { "∞", "vô cực" }, new[] { "°", "độ (gõ ^o)" }, new[] { "∠", "góc" },
            new[] { "△", "tam giác" }, new[] { "⊥", "vuông góc" }, new[] { "∥", "song song" }, new[] { "∈", "thuộc" },
            new[] { "⊂", "tập con" }, new[] { "∪", "hợp" }, new[] { "∩", "giao" }, new[] { "→", "mũi tên (gõ ->)" },
            new[] { "⇒", "suy ra (gõ =>)" }, new[] { "α", "an-pha" }, new[] { "Δ", "đen-ta" }, new[] { "½", "một phần hai" },
        };

        readonly float k;
        readonly ToolTip tip = new ToolTip { InitialDelay = 300, ReshowDelay = 100 };
        int hover = -1;
        public event Action<string> Picked;

        public SymbolRow(float k)
        {
            this.k = k;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Paper;
            Cursor = Cursors.Hand;
        }

        int S(double v) { return (int)Math.Round(v * k); }
        int Lead { get { return S(96); } }
        int KeyW { get { return Math.Max(S(28), (Width - Lead) / Signs.Length - S(3)); } }

        Rectangle KeyRect(int i) { return new Rectangle(Lead + i * (KeyW + S(3)), 0, KeyW, Height - 1); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var f = Fonts.Get("uiBold", S(12)))
                Ui.Text(g, "KÝ HIỆU TOÁN", f, Theme.Green, new Rectangle(0, 0, Lead - S(6), Height), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            using (var sym = new Font("Segoe UI Symbol", S(17), FontStyle.Regular, GraphicsUnit.Pixel))
                for (int i = 0; i < Signs.Length; i++)
                {
                    var r = KeyRect(i);
                    using (var p = Ui.Box(r, S(8), true))
                    {
                        using (var b = new SolidBrush(hover == i ? Theme.GreenSoft : Color.White)) g.FillPath(b, p);
                        using (var pen = new Pen(hover == i ? Theme.Green : Theme.Line, 1)) g.DrawPath(pen, p);
                    }
                    Ui.Text(g, Signs[i][0], sym, hover == i ? Theme.Green : Theme.Ink, r, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
        }

        int Hit(Point p) { for (int i = 0; i < Signs.Length; i++) if (KeyRect(i).Contains(p)) return i; return -1; }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = Hit(e.Location);
            if (h == hover) return;
            hover = h;
            tip.SetToolTip(this, h >= 0 ? Signs[h][0] + "  " + Signs[h][1] : null);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); }
        protected override void OnMouseClick(MouseEventArgs e) { int h = Hit(e.Location); if (h >= 0 && Picked != null) Picked(Signs[h][0]); }
        protected override void Dispose(bool disposing) { if (disposing) tip.Dispose(); base.Dispose(disposing); }
    }

    /// <summary>Font chips, size segments and chalk colours in one owner-drawn row.</summary>
    class ChipRow : Control
    {
        readonly float k;
        readonly List<Tuple<Rectangle, string, string>> hits = new List<Tuple<Rectangle, string, string>>();
        int hover = -1;
        public event Action Changed;

        public ChipRow(float scale)
        {
            k = scale;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Paper;
            Cursor = Cursors.Hand;
        }

        int S(double v) { return (int)Math.Round(v * k); }

        static readonly string[] ChalkColors = { "white", "yellow", "pink", "blue", "red", "purple" };

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            hits.Clear();
            int x = 0, h = Height;
            using (var label = Fonts.Get("uiSemi", S(12)))
            {
                foreach (var f in Catalog.TextFonts)
                {
                    var r = new Rectangle(x, 2, S(88), h - 4);
                    bool on = Settings.TextFont == f[0];
                    Tile(g, r, on, hits.Count == hover);
                    using (var font = Fonts.Get(f[0], S(20)))
                        Ui.Text(g, "Aă", font, Theme.Green, new Rectangle(r.X, r.Y + S(1), r.Width, S(34)), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    Ui.Text(g, f[1], label, on ? Theme.Green : Theme.Text, new Rectangle(r.X, r.Bottom - S(21), r.Width, S(17)), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    hits.Add(Tuple.Create(r, "font", f[0]));
                    x += r.Width + S(6);
                }
                x += S(10);
                var seg = new Rectangle(x, S(8), S(4 * 56 + 8), h - S(16));
                using (var p = Draw.Round(seg, S(12))) using (var b = new SolidBrush(Theme.Tray)) g.FillPath(b, p);
                int sx = seg.X + S(4);
                using (var f = Fonts.Get("uiSemi", S(13)))
                    foreach (var s in Catalog.TextSizes)
                    {
                        var r = new Rectangle(sx, seg.Y + S(4), S(56), seg.Height - S(8));
                        bool on = Settings.TextSize == s[0];
                        if (on) using (var p = Draw.Round(r, S(9))) using (var b = new SolidBrush(Color.White)) g.FillPath(b, p);
                        else if (hits.Count == hover) using (var p = Draw.Round(r, S(9))) using (var b = new SolidBrush(Theme.Hover)) g.FillPath(b, p);
                        Ui.Text(g, s[1], f, on ? Theme.Green : Theme.Text, r, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                        hits.Add(Tuple.Create(r, "size", s[0]));
                        sx += S(56);
                    }
                x = seg.Right + S(16);
                foreach (var c in ChalkColors)
                {
                    var hex = Catalog.Inks.First(i => i[0] == c)[2];
                    var r = new Rectangle(x, (h - S(34)) / 2, S(34), S(34));
                    bool on = Settings.InkChoice == c;
                    using (var b = new SolidBrush(Catalog.Hex(hex))) g.FillEllipse(b, Rectangle.Inflate(r, -S(4), -S(4)));
                    using (var p = new Pen(on ? Theme.Green : Theme.LineStrong, on ? S(3) : 1)) g.DrawEllipse(p, Rectangle.Inflate(r, -S(on ? 1.5 : 4), -S(on ? 1.5 : 4)));
                    hits.Add(Tuple.Create(r, "ink", c));
                    x += S(38);
                }
            }
        }

        void Tile(Graphics g, Rectangle r, bool on, bool hov)
        {
            using (var p = Ui.Box(r, S(12), true))
            {
                using (var b = new SolidBrush(on ? Theme.GreenSoft : hov ? Theme.Hover : Color.White)) g.FillPath(b, p);
                using (var pen = new Pen(on ? Theme.Green : Theme.Line, on ? S(2) : 1)) g.DrawPath(pen, p);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = hits.FindIndex(t => t.Item1.Contains(e.Location));
            if (h != hover) { hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            var t = hits.FirstOrDefault(x => x.Item1.Contains(e.Location));
            if (t == null) return;
            if (t.Item2 == "font") Settings.TextFont = t.Item3;
            else if (t.Item2 == "size") Settings.TextSize = t.Item3;
            else Settings.InkChoice = t.Item3;
            Invalidate();
            if (Changed != null) Changed();
        }
    }

    /// <summary>Maths short cuts typed on the keyboard (^2 → ², >= → ≥, sqrt → √ …), applied at the caret after each keystroke.</summary>
    public static class MathInput
    {        /// <summary>Typed short cuts, checked at the caret after every keystroke.</summary>
        public static readonly string[][] Rules = BuildRules();

        static string[][] BuildRules()
        {
            var list = new List<string[]>
            {
                new[] { "≤>", "⇔" }, new[] { ">=", "≥" }, new[] { "<=", "≤" }, new[] { "!=", "≠" }, new[] { "+-", "±" },
                new[] { "~~", "≈" }, new[] { "->", "→" }, new[] { "=>", "⇒" }, new[] { "sqrt", "√" }, new[] { "*", "×" },
                new[] { "^+", "⁺" }, new[] { "^-", "⁻" }, new[] { "^n", "ⁿ" }, new[] { "^o", "°" },
            };
            const string sup = "⁰¹²³⁴⁵⁶⁷⁸⁹", sub = "₀₁₂₃₄₅₆₇₈₉";
            for (int d = 0; d < 10; d++)
            {
                list.Add(new[] { "^" + d, sup[d].ToString() });
                list.Add(new[] { "_" + d, sub[d].ToString() });
            }
            return list.ToArray();
        }

        /// <summary>If the text just before the caret ends with a short cut, returns the text with it replaced and the new caret.</summary>
        public static bool Apply(string text, int caret, out string result, out int newCaret)
        {
            result = text; newCaret = caret;
            if (string.IsNullOrEmpty(text) || caret <= 0 || caret > text.Length) return false;
            string before = text.Substring(0, caret);
            foreach (var r in Rules)
            {
                if (!before.EndsWith(r[0], StringComparison.Ordinal)) continue;
                result = before.Substring(0, before.Length - r[0].Length) + r[1] + text.Substring(caret);
                newCaret = caret - r[0].Length + r[1].Length;
                return true;
            }
            return false;
        }
    }}
