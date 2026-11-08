using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PhanTrang
{
    /// <summary>One set of corner radii and one type scale, used by every window.</summary>
    public static class Ui
    {
        public const float RadiusSmall = 10, RadiusMedium = 14, RadiusLarge = 20;

        /// <summary>Type scale (px at 96 dpi): page title · section · item title · body · caption.</summary>
        public static Font Title(float k) { return Fonts.Get("uiBold", 22 * k); }
        public static Font Section(float k) { return Fonts.Get("uiBold", 13 * k); }
        public static Font ItemTitle(float k) { return Fonts.Get("uiSemi", 15 * k); }
        public static Font Body(float k) { return Fonts.Get("ui", 14 * k); }
        public static Font Caption(float k) { return Fonts.Get("ui", 12.5f * k); }
        public static Font Button(float k) { return Fonts.Get("uiSemi", 14 * k); }

        /// <summary>
        /// Window text through GDI (ClearType, hinted to the pixel grid): noticeably sharper than GDI+ DrawString.
        /// Only for opaque surfaces (window controls), not for the layered overlay.
        /// </summary>
        public static void Text(Graphics g, string s, Font f, Color c, Rectangle r, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, s, f, r, c, flags | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        public static Size Measure(string s, Font f, int width = int.MaxValue, bool wrap = false)
        {
            var flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | (wrap ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine);
            return TextRenderer.MeasureText(s, f, new Size(width, int.MaxValue), flags);
        }

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        /// <summary>Ask Windows 11 to draw smooth rounded corners on a borderless window (no jagged Region).</summary>
        public static void RoundWindow(IntPtr hwnd)
        {
            try { int round = 2; DwmSetWindowAttribute(hwnd, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref round, 4); } catch { }
        }

        /// <summary>Windows 11: paint the title bar cream to match the window (older Windows ignores it).</summary>
        public static void PaperCaption(IntPtr hwnd)
        {
            try
            {
                int caption = Theme.Cream.R | (Theme.Cream.G << 8) | (Theme.Cream.B << 16);
                int text = Theme.Ink.R | (Theme.Ink.G << 8) | (Theme.Ink.B << 16);
                int border = Theme.LineStrong.R | (Theme.LineStrong.G << 8) | (Theme.LineStrong.B << 16);
                DwmSetWindowAttribute(hwnd, 35 /* DWMWA_CAPTION_COLOR */, ref caption, 4);
                DwmSetWindowAttribute(hwnd, 36 /* DWMWA_TEXT_COLOR */, ref text, 4);
                DwmSetWindowAttribute(hwnd, 34 /* DWMWA_BORDER_COLOR */, ref border, 4);
            }
            catch { }
        }

        /// <summary>A rounded rectangle path inset by half a pixel so a 1 px border lands on whole pixels.</summary>
        public static GraphicsPath Box(RectangleF r, float radius, bool forStroke)
        {
            if (forStroke) r = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1);
            return Draw.Round(r, radius);
        }
    }

    /// <summary>Rounded button: primary (green), secondary (white) or a sidebar item with an icon.</summary>
    class RoundButton : Control
    {
        public enum Kind { Primary, Secondary, Nav }
        readonly Kind kind;
        readonly float k;
        bool hover, down;
        public bool Selected;
        /// <summary>ToolIcons name drawn on the left of a sidebar item.</summary>
        public string Icon;

        public RoundButton(string text, Kind kind, float k)
        {
            this.kind = kind;
            this.k = k;
            Text = text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = Ui.Button(k);
            Height = (int)Math.Round((kind == Kind.Nav ? 44 : 44) * k);
            if (kind != Kind.Nav) Width = Ui.Measure(text, Font).Width + (int)Math.Round(40 * k);
            TabStop = true;
            AccessibleName = text;
            AccessibleRole = AccessibleRole.PushButton;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); AccessibleName = Text; base.OnTextChanged(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (BackColor.A == 0 && Parent != null) g.Clear(Parent.BackColor);
            Color fill, edge, fg;
            if (kind == Kind.Primary) { fill = down ? Color.FromArgb(22, 58, 46) : hover ? Color.FromArgb(42, 99, 80) : Theme.Green; edge = fill; fg = Theme.Chalk; }
            else if (kind == Kind.Secondary) { fill = down ? Theme.Hover : hover ? Theme.Paper : Color.White; edge = hover ? Theme.Green : Theme.LineStrong; fg = Theme.Ink; }
            else { fill = Selected ? Color.White : hover ? Theme.Hover : Color.Transparent; edge = Selected ? Theme.Line : fill; fg = Selected ? Theme.Green : Theme.Text; }
            if (!Enabled) fg = Color.FromArgb(150, fg);
            float radius = Ui.RadiusSmall * k;
            using (var p = Ui.Box(new RectangleF(0, 0, Width, Height), radius, true))
            {
                if (fill.A > 0) using (var b = new SolidBrush(fill)) g.FillPath(b, p);
                if (edge.A > 0 && kind != Kind.Primary) using (var pen = new Pen(edge, 1)) g.DrawPath(pen, p);
            }
            if (Focused && ShowFocusCues)
                using (var p = Ui.Box(new RectangleF(2 * k, 2 * k, Width - 4 * k, Height - 4 * k), radius - 2 * k, true))
                using (var pen = new Pen(Theme.Yellow, 2 * k)) g.DrawPath(pen, p);
            if (kind == Kind.Nav)
            {
                int x = (int)Math.Round(14 * k);
                if (Icon != null)
                {
                    int s = 2 * (int)Math.Round(10 * k);
                    ToolIcons.Draw(g, Icon, new Rectangle(x, (Height - s) / 2, s, s), fg, false);
                    x += s + (int)Math.Round(12 * k);
                }
                using (var f = Fonts.Get(Selected ? "uiBold" : "uiSemi", 14.5f * k))
                    Ui.Text(g, Text, f, fg, new Rectangle(x, 0, Width - x - (int)(8 * k), Height), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                return;
            }
            Ui.Text(g, Text, Font, fg, new Rectangle(0, 0, Width, Height), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>A white rounded card: title, an explanation that wraps, and a control on the right.
    /// Its height follows the text so long explanations never spill out.</summary>
    class Card : Control
    {
        readonly string title, desc;
        readonly float k;
        readonly Control right;

        public Card(string title, string desc, Control right, int width, float k)
        {
            this.title = title; this.desc = desc; this.right = right; this.k = k;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Cream;
            Width = width;
            Margin = new Padding(0, 0, 0, (int)(8 * k));
            right.BackColor = Color.White;
            Controls.Add(right);
            Height = Measure();
            right.Location = new Point(Width - right.Width - (int)(18 * k), (Height - right.Height) / 2);
            AccessibleName = title;
        }

        int TextWidth { get { return Width - right.Width - (int)(54 * k); } }

        int TitleH(Font tf) { return Ui.Measure(title, tf, TextWidth, true).Height; }
        int DescH(Font df) { return desc == null ? 0 : Ui.Measure(desc, df, TextWidth, true).Height; }

        int Measure()
        {
            using (var tf = Ui.ItemTitle(k))
            using (var df = Ui.Caption(k))
            {
                float h = TitleH(tf) + (desc == null ? 0 : 3 * k + DescH(df));
                return (int)Math.Ceiling(Math.Max(h + 32 * k, right.Height + 28 * k));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            using (var p = Ui.Box(new RectangleF(0, 0, Width, Height), Ui.RadiusMedium * k, true))
            {
                using (var b = new SolidBrush(Color.White)) g.FillPath(b, p);
                using (var pen = new Pen(Theme.Line, 1)) g.DrawPath(pen, p);
            }
            using (var tf = Ui.ItemTitle(k))
            using (var df = Ui.Caption(k))
            {
                int th = TitleH(tf), dh = DescH(df);
                int y = (int)Math.Round((Height - th - (desc == null ? 0 : dh + 3 * k)) / 2);
                int x = (int)Math.Round(18 * k);
                Ui.Text(g, title, tf, Theme.Ink, new Rectangle(x, y, TextWidth, th), TextFormatFlags.WordBreak);
                if (desc != null) Ui.Text(g, desc, df, Theme.Dim, new Rectangle(x, y + th + (int)Math.Round(3 * k), TextWidth, dh), TextFormatFlags.WordBreak);
            }
        }
    }

    /// <summary>The rounded white field (same as SelectBox) around a borderless text box, e.g. a shortcut recorder.</summary>
    class FieldHost : Control
    {
        readonly Control inner;
        readonly float k;
        public FieldHost(TextBox inner, float k)
        {
            this.inner = inner; this.k = k;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Size = new Size((int)Math.Round(210 * k), (int)Math.Round(42 * k));
            inner.BorderStyle = BorderStyle.None;
            inner.Width = Width - (int)Math.Round(24 * k);
            inner.Location = new Point((int)Math.Round(12 * k), (Height - inner.Height) / 2);
            Controls.Add(inner);
            Cursor = Cursors.Hand;
            inner.GotFocus += (s, e) => Invalidate();
            inner.LostFocus += (s, e) => Invalidate();
            Click += (s, e) => inner.Focus();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            bool f = inner.Focused;
            using (var p = Ui.Box(new RectangleF(0, 0, Width, Height), Ui.RadiusSmall * k, true))
            {
                using (var b = new SolidBrush(Color.White)) g.FillPath(b, p);
                using (var pen = new Pen(f ? Theme.Green : Theme.LineStrong, f ? 2 : 1)) g.DrawPath(pen, p);
            }
        }
    }

    /// <summary>Menus (tray, drop-downs) on cream paper with the green selection, instead of the grey Windows look.</summary>
    class PaperMenuRenderer : ToolStripProfessionalRenderer
    {
        public PaperMenuRenderer() : base(new PaperColors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item.Selected && e.Item.Enabled && !(e.Item is ToolStripLabel)) e.TextColor = Theme.Green;
            else if (!e.Item.Enabled) e.TextColor = Color.FromArgb(150, 143, 128);
            else if (e.Item.ForeColor == SystemColors.ControlText || e.Item.ForeColor == Color.Empty) e.TextColor = Theme.Ink;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = e.ImageRectangle;
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, s = Math.Min(r.Width, r.Height) * 0.32f;
            using (var pen = new Pen(Theme.Green, Math.Max(2, s * 0.45f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen, new[] { new PointF(cx - s, cy), new PointF(cx - s * 0.3f, cy + s * 0.7f), new PointF(cx + s, cy - s * 0.7f) });
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor = Theme.Text; base.OnRenderArrow(e); }

        class PaperColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Theme.Paper; } }
            public override Color ImageMarginGradientBegin { get { return Theme.Paper; } }
            public override Color ImageMarginGradientMiddle { get { return Theme.Paper; } }
            public override Color ImageMarginGradientEnd { get { return Theme.Paper; } }
            public override Color MenuBorder { get { return Theme.LineStrong; } }
            public override Color MenuItemBorder { get { return Theme.GreenSoft; } }
            public override Color MenuItemSelected { get { return Theme.GreenSoft; } }
            public override Color MenuItemSelectedGradientBegin { get { return Theme.GreenSoft; } }
            public override Color MenuItemSelectedGradientEnd { get { return Theme.GreenSoft; } }
            public override Color SeparatorDark { get { return Theme.Line; } }
            public override Color SeparatorLight { get { return Theme.Paper; } }
            public override Color CheckBackground { get { return Theme.Paper; } }
            public override Color CheckSelectedBackground { get { return Theme.GreenSoft; } }
            public override Color CheckPressedBackground { get { return Theme.GreenSoft; } }
        }
    }

    /// <summary>A drop-down in the Phấn Trắng look (the Windows combo box looked out of place): opens a menu of choices.</summary>
    class SelectBox : Control
    {
        readonly string[] labels;
        readonly Action<int> set;
        readonly float k;
        int sel;
        bool hover;

        public SelectBox(string[] labels, int selected, Action<int> set, float k)
        {
            this.labels = labels; this.set = set; this.k = k;
            sel = Math.Max(0, Math.Min(labels.Length - 1, selected));
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            Size = new Size((int)Math.Round(210 * k), (int)Math.Round(42 * k));
            Font = Ui.Body(k);
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.ComboBox;
            AccessibleDescription = labels[sel];
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        void Choose(int i)
        {
            if (i < 0 || i >= labels.Length || i == sel) return;
            sel = i;
            AccessibleDescription = labels[sel];
            Invalidate();
            set(i);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Focus();
            var menu = new ContextMenuStrip { Font = Font, ShowImageMargin = false, ShowCheckMargin = true, Renderer = new PaperMenuRenderer() };
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var it = new ToolStripMenuItem(labels[i]) { Checked = i == sel, Padding = new Padding(0, (int)(4 * k), 0, (int)(4 * k)) };
                it.Click += (s, ev) => Choose(idx);
                menu.Items.Add(it);
            }
            menu.MinimumSize = new Size(Width, 0);
            menu.Closed += (s, ev) => menu.BeginInvoke(new Action(menu.Dispose));
            menu.Show(this, new Point(0, Height + (int)(2 * k)));
        }

        protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down) { Choose(sel + 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Up) { Choose(sel - 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space || (e.Alt && e.KeyCode == Keys.Down)) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            using (var p = Ui.Box(new RectangleF(0, 0, Width, Height), Ui.RadiusSmall * k, true))
            {
                using (var b = new SolidBrush(Color.White)) g.FillPath(b, p);
                using (var pen = new Pen(hover || Focused ? Theme.Green : Theme.LineStrong, Focused ? 2 : 1)) g.DrawPath(pen, p);
            }
            int pad = (int)Math.Round(14 * k), chev = (int)Math.Round(30 * k);
            Ui.Text(g, labels[sel], Font, Theme.Ink, new Rectangle(pad, 0, Width - pad - chev, Height), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            float cx = Width - chev / 2f - 2 * k, cy = Height / 2f, s = 4.5f * k;
            using (var pen = new Pen(Theme.Text, Math.Max(1.5f, 2 * k)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen, new[] { new PointF(cx - s, cy - s / 2), new PointF(cx, cy + s / 2), new PointF(cx + s, cy - s / 2) });
        }
    }
}
