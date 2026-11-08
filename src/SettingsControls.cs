using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace PhanTrang
{
    /// <summary>
    /// One group of choices drawn as tiles (with a picture), a segmented strip, or colour dots.
    /// Mouse or keyboard: Tab into the group, arrows move, Space or Enter picks.
    /// </summary>
    class ChoiceGrid : Control
    {
        public enum Look { Tiles, Strip, Dots }

        readonly Look look;
        readonly string[][] options;               // [id, label, (colour hex for dots)]
        readonly Func<string, bool> chosen;
        readonly Action<string> choose;
        readonly Action<Graphics, Rectangle, string> picture;
        readonly float k;
        readonly int perRow, cellH;
        bool several;                              // tiles that toggle independently (a tick marks the chosen ones)
        int hot = -1, cursor = -1;

        public ChoiceGrid(Look look, string[][] options, Func<string, bool> chosen, Action<string> choose, float k, int width,
                          int perRow = 0, int cellHeight = 0, Action<Graphics, Rectangle, string> picture = null)
        {
            this.look = look; this.options = options; this.chosen = chosen; this.choose = choose; this.k = k; this.picture = picture;
            this.perRow = look == Look.Strip ? options.Length : perRow > 0 ? perRow : options.Length;
            cellH = cellHeight > 0 ? cellHeight : look == Look.Strip ? 44 : look == Look.Dots ? 46 : 96;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            TabStop = true;
            BackColor = Theme.Cream;
            Cursor = Cursors.Hand;
            Width = width;
            int rows = (options.Length + this.perRow - 1) / this.perRow;
            switch (look)
            {
                case Look.Strip: Height = Px(cellH + 8); break;
                case Look.Dots: Height = Px(cellH) + Px(22); break;
                default: Height = rows * Px(cellH) + (rows - 1) * Px(10); break;
            }
            Margin = new Padding(0, Px(2), 0, Px(6));
        }

        /// <summary>Lets several tiles be on at once.</summary>
        public ChoiceGrid Several() { several = true; return this; }

        int Px(double v) { return (int)Math.Round(v * k); }

        Rectangle Cell(int i)
        {
            switch (look)
            {
                case Look.Strip:
                    int w = (Width - Px(8)) / options.Length;
                    return new Rectangle(Px(4) + i * w, Px(4), w, Px(cellH));
                case Look.Dots:
                    return new Rectangle(i * Px(cellH + 6), 0, Px(cellH), Px(cellH));
                default:
                    int gap = Px(10), tileW = (Width - gap * (perRow - 1)) / perRow;
                    return new Rectangle(i % perRow * (tileW + gap), i / perRow * (Px(cellH) + gap), tileW, Px(cellH));
            }
        }

        const TextFormatFlags Centred = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (look == Look.Strip)
                using (var track = Draw.Round(new RectangleF(0, 0, Width - 1, Px(cellH + 8)), Px(12)))
                using (var fill = new SolidBrush(Theme.Tray)) g.FillPath(fill, track);
            using (var font = Fonts.Get("uiSemi", Px(13)))
                for (int i = 0; i < options.Length; i++)
                {
                    bool on = chosen(options[i][0]);
                    if (look == Look.Strip) PaintSegment(g, i, on, font);
                    else if (look == Look.Dots) PaintDot(g, i, on);
                    else PaintTile(g, i, on, font);
                }
            PaintFocus(g);
            if (look == Look.Dots)
            {
                var current = options.FirstOrDefault(o => chosen(o[0]));
                if (current != null)
                    using (var f = Fonts.Get("ui", Px(12.5f)))
                        Ui.Text(g, "Đang chọn: " + current[1], f, Theme.Dim, new Rectangle(0, Px(cellH + 2), Width, Px(20)), TextFormatFlags.Left);
            }
        }

        void PaintSegment(Graphics g, int i, bool on, Font font)
        {
            var r = Cell(i);
            if (on || hot == i)
                using (var pill = Draw.Round(r, Px(9))) using (var b = new SolidBrush(on ? Color.White : Theme.Hover)) g.FillPath(b, pill);
            Ui.Text(g, options[i][1], font, on ? Theme.Green : Theme.Text, r, Centred);
        }

        void PaintDot(Graphics g, int i, bool on)
        {
            var r = Cell(i);
            using (var ink = new SolidBrush(Catalog.Hex(options[i][2]))) g.FillEllipse(ink, Rectangle.Inflate(r, -Px(5), -Px(5)));
            float ring = on ? Px(1.5) : Px(5);
            using (var edge = new Pen(on ? Theme.Green : Theme.LineStrong, on ? Px(3) : 1)) g.DrawEllipse(edge, RectangleF.Inflate(r, -ring, -ring));
        }

        void PaintTile(Graphics g, int i, bool on, Font font)
        {
            var r = Cell(i);
            using (var shape = Ui.Box(r, Px(14), true))
            {
                using (var fill = new SolidBrush(on ? Theme.GreenSoft : hot == i ? Theme.Paper : Color.White)) g.FillPath(fill, shape);
                using (var edge = new Pen(on ? Theme.Green : hot == i ? Theme.LineStrong : Theme.Line, on ? Px(2) : 1)) g.DrawPath(edge, shape);
            }
            var art = new Rectangle(r.X + Px(8), r.Y + Px(8), r.Width - Px(16), r.Height - Px(36));
            if (picture != null) picture(g, art, options[i][0]);
            if (several)
            {
                if (on) PaintTick(g, r);
                else using (var veil = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillRectangle(veil, art);
            }
            Ui.Text(g, options[i][1], font, on ? Theme.Green : Theme.Text, new Rectangle(r.X + 2, r.Bottom - Px(27), r.Width - 4, Px(20)), Centred);
        }

        void PaintTick(Graphics g, Rectangle tile)
        {
            var badge = new RectangleF(tile.Right - Px(28), tile.Y + Px(8), Px(20), Px(20));
            using (var green = new SolidBrush(Theme.Green)) g.FillEllipse(green, badge);
            using (var pen = Geo.RoundPen(Color.White, Px(2)))
                g.DrawLines(pen, new[] { new PointF(badge.X + Px(5), badge.Y + Px(10.5)), new PointF(badge.X + Px(8.5), badge.Y + Px(14)), new PointF(badge.X + Px(15), badge.Y + Px(6.5)) });
        }

        void PaintFocus(Graphics g)
        {
            if (!Focused || cursor < 0 || !ShowFocusCues) return;
            var r = Rectangle.Inflate(Cell(cursor), -1, -1);
            float radius = look == Look.Dots ? r.Width / 2f : Px(look == Look.Strip ? 9 : 14);
            using (var ring = Ui.Box(r, radius, true)) using (var pen = new Pen(Theme.Yellow, Px(2.5))) g.DrawPath(pen, ring);
        }

        int At(Point p)
        {
            for (int i = 0; i < options.Length; i++) if (Cell(i).Contains(p)) return i;
            return -1;
        }

        void Pick(int i) { choose(options[i][0]); Invalidate(); }

        protected override void OnMouseMove(MouseEventArgs e) { int i = At(e.Location); if (i != hot) { hot = i; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { hot = -1; Invalidate(); }
        protected override void OnMouseClick(MouseEventArgs e) { int i = At(e.Location); if (i < 0) return; cursor = i; Focus(); Pick(i); }

        protected override bool IsInputKey(Keys key) { return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || base.IsInputKey(key); }

        protected override void OnGotFocus(EventArgs e)
        {
            if (cursor < 0) cursor = Math.Max(0, Array.FindIndex(options, o => chosen(o[0])));
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int rowStep = look == Look.Tiles ? perRow : 1;
            int move = e.KeyCode == Keys.Left ? -1 : e.KeyCode == Keys.Right ? 1 : e.KeyCode == Keys.Up ? -rowStep : e.KeyCode == Keys.Down ? rowStep : 0;
            if (move != 0)
            {
                cursor = Math.Max(0, Math.Min(options.Length - 1, cursor + move));
                if (!several) Pick(cursor);   // a single choice follows the arrows, like radio buttons
                else Invalidate();
                e.Handled = true;
            }
            else if ((e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) && cursor >= 0) { Pick(cursor); e.Handled = true; }
            base.OnKeyDown(e);
        }
    }

    /// <summary>An on/off switch in the style of a phone toggle.</summary>
    class ToggleSwitch : Control
    {
        bool on;
        readonly float k;
        public event Action<bool> Toggled;

        public ToggleSwitch(bool value, float k)
        {
            on = value;
            this.k = k;
            Size = new Size((int)(54 * k), (int)(34 * k));
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            BackColor = Color.White;
            TabStop = true;
            AccessibleRole = AccessibleRole.CheckButton;
        }

        public bool Checked { get { return on; } set { on = value; Invalidate(); } }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            if (Focused && ShowFocusCues)
                using (var ring = Draw.Round(new RectangleF(1, 1, Width - 2, Height - 2), (Height - 2) / 2f)) using (var pen = new Pen(Theme.Yellow, 2 * k)) g.DrawPath(pen, ring);
            float m = 3 * k;
            var track = new RectangleF(m, m, Width - 2 * m, Height - 2 * m);
            using (var shape = Draw.Round(track, track.Height / 2f))
            using (var fill = new SolidBrush(on ? Theme.Green : Color.FromArgb(201, 193, 174))) g.FillPath(fill, shape);
            float knob = track.Height - 8 * k, x = on ? track.Right - knob - 4 * k : track.Left + 4 * k;
            using (var white = new SolidBrush(Color.White)) g.FillEllipse(white, x, track.Top + 4 * k, knob, knob);
        }

        void Flip()
        {
            on = !on;
            AccessibleDescription = on ? "Đang bật" : "Đang tắt";
            Invalidate();
            if (Toggled != null) Toggled(on);
        }

        protected override void OnClick(EventArgs e) { Focus(); Flip(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { Flip(); e.Handled = true; }
            base.OnKeyDown(e);
        }
    }

    /// <summary>A box that records a new global shortcut. The app's shortcuts are paused while it has focus, so the old one can be typed.</summary>
    class ShortcutBox : TextBox
    {
        KeyCombo combo;
        readonly TeacherApp app;
        public event Action<KeyCombo> Changed;

        public ShortcutBox(KeyCombo initial, TeacherApp app)
        {
            this.app = app;
            combo = initial;
            ReadOnly = true;
            BackColor = Color.White;
            TextAlign = HorizontalAlignment.Center;
            Cursor = Cursors.Hand;
            ShortcutsEnabled = false;
            Text = combo.Display;
        }

        protected override void OnEnter(EventArgs e) { base.OnEnter(e); app.PauseShortcuts(true); Text = "Nhấn phím…"; }
        protected override void OnLeave(EventArgs e) { base.OnLeave(e); Text = combo.Display; app.PauseShortcuts(false); }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if ((keyData & Keys.KeyCode) == Keys.Tab) return base.ProcessCmdKey(ref msg, keyData);
            Take(keyData);
            return true;
        }

        protected override void OnKeyDown(KeyEventArgs e) { e.SuppressKeyPress = true; e.Handled = true; Take(e.KeyData); }

        static readonly Keys[] ModifierKeys_ = { Keys.ControlKey, Keys.Menu, Keys.ShiftKey, Keys.LWin, Keys.RWin };

        void Take(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (ModifierKeys_.Contains(key)) return;   // wait for the real key
            uint mods = ((keyData & Keys.Control) != 0 ? Win32.Mod.Ctrl : 0)
                      | ((keyData & Keys.Alt) != 0 ? Win32.Mod.Alt : 0)
                      | ((keyData & Keys.Shift) != 0 ? Win32.Mod.Shift : 0)
                      | (Win32.KeyDown((int)Keys.LWin) || Win32.KeyDown((int)Keys.RWin) ? Win32.Mod.Win : 0);
            if (mods == 0)
            {
                if (key == Keys.Back || key == Keys.Delete) Accept(new KeyCombo(0, Keys.None));   // no shortcut at all
                else if (key == Keys.Escape) { Text = combo.Display; LetGo(); }
                else Text = "Cần kèm Ctrl, Alt, Shift hoặc Win";
                return;
            }
            Accept(new KeyCombo(mods, key));
        }

        void LetGo() { var form = FindForm(); if (form != null) form.ActiveControl = null; }

        void Accept(KeyCombo c)
        {
            combo = c;
            Text = c.Display;
            if (Changed != null) Changed(c);
            LetGo();
        }
    }

    /// <summary>The top of a settings page: the section icon in a soft green circle, the title and a line on what it is for.</summary>
    class PageHeader : Control
    {
        readonly string icon, title, sub;
        readonly float k;

        public PageHeader(string icon, string title, string sub, int width, float k)
        {
            this.icon = icon; this.title = title; this.sub = sub; this.k = k;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Cream;
            Width = width;
            Height = Math.Max(Px(56), TextHeight(width - Px(72))) + Px(14);
            AccessibleName = title;
            AccessibleRole = AccessibleRole.Grouping;
        }

        int Px(double v) { return (int)Math.Round(v * k); }

        int TextHeight(int w)
        {
            using (var tf = Ui.Title(k)) using (var sf = Ui.Body(k))
                return Ui.Measure(title, tf, w, true).Height + (sub == null ? 0 : Px(4) + Ui.Measure(sub, sf, w, true).Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            int circle = Px(56), iconSize = 2 * Px(13);
            using (var soft = new SolidBrush(Theme.GreenSoft)) g.FillEllipse(soft, 0, 0, circle, circle);
            ToolIcons.Draw(g, icon, new Rectangle((circle - iconSize) / 2, (circle - iconSize) / 2, iconSize, iconSize), Theme.Green, false);
            int x = Px(72), w = Width - x;
            using (var tf = Ui.Title(k)) using (var sf = Ui.Body(k))
            {
                int titleH = Ui.Measure(title, tf, w, true).Height;
                int subH = sub == null ? 0 : Ui.Measure(sub, sf, w, true).Height;
                int y = Math.Max(0, (circle - titleH - (sub == null ? 0 : Px(4) + subH)) / 2);
                Ui.Text(g, title, tf, Theme.Ink, new Rectangle(x, y, w, titleH), TextFormatFlags.WordBreak);
                if (sub != null) Ui.Text(g, sub, sf, Theme.Dim, new Rectangle(x, y + titleH + Px(4), w, subH), TextFormatFlags.WordBreak);
            }
            using (var rule = new Pen(Theme.Line)) g.DrawLine(rule, 0, Height - 1, Width, Height - 1);
        }
    }

    /// <summary>A white rounded card of setting rows: title and explanation on the left, a control on the right, thin lines between rows.</summary>
    class SettingsCard : Control
    {
        sealed class Line { public string Title, Desc; public Control Control; public int Top, Height; }

        readonly List<Line> lines = new List<Line>();
        readonly float k;
        int hot = -1;

        public SettingsCard(int width, float k)
        {
            this.k = k;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Cream;
            Width = width;
            Height = 2;
        }

        int Px(double v) { return (int)Math.Round(v * k); }
        int TextWidth(Control c) { return Width - c.Width - Px(58); }

        int TextHeight(Line l, Font tf, Font df, out int titleH, out int descH)
        {
            int w = TextWidth(l.Control);
            titleH = Ui.Measure(l.Title, tf, w, true).Height;
            descH = l.Desc == null ? 0 : Ui.Measure(l.Desc, df, w, true).Height;
            return titleH + (l.Desc == null ? 0 : Px(3) + descH);
        }

        public void AddRow(string title, string desc, Control control)
        {
            var line = new Line { Title = title, Desc = desc, Control = control, Top = lines.Count == 0 ? 0 : lines[lines.Count - 1].Top + lines[lines.Count - 1].Height };
            using (var tf = Ui.ItemTitle(k)) using (var df = Ui.Caption(k))
            {
                int th, dh;
                line.Height = Math.Max(TextHeight(line, tf, df, out th, out dh) + Px(30), control.Height + Px(24));
            }
            lines.Add(line);
            control.BackColor = Color.White;
            control.Location = new Point(Width - control.Width - Px(18), line.Top + (line.Height - control.Height) / 2);
            if (control.AccessibleName == null) control.AccessibleName = title;
            control.MouseEnter += (s, e) => Hot(lines.IndexOf(line));
            Controls.Add(control);
            Height = line.Top + line.Height;
            Invalidate();
        }

        void Hot(int i) { if (i != hot) { hot = i; Invalidate(); } }

        protected override void OnMouseMove(MouseEventArgs e) { Hot(lines.FindIndex(l => e.Y >= l.Top && e.Y < l.Top + l.Height)); base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { if (!ClientRectangle.Contains(PointToClient(Cursor.Position))) Hot(-1); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            using (var card = Ui.Box(new RectangleF(0, 0, Width, Height), Ui.RadiusMedium * k, true))
            {
                using (var white = new SolidBrush(Color.White)) g.FillPath(white, card);
                if (hot >= 0)
                {
                    var saved = g.Save();
                    g.SetClip(card);
                    using (var tint = new SolidBrush(Theme.Paper)) g.FillRectangle(tint, 0, lines[hot].Top, Width, lines[hot].Height);
                    g.Restore(saved);
                }
                using (var edge = new Pen(Theme.Line, 1)) g.DrawPath(edge, card);
            }
            using (var tf = Ui.ItemTitle(k)) using (var df = Ui.Caption(k)) using (var rule = new Pen(Theme.Line, 1))
                for (int i = 0; i < lines.Count; i++)
                {
                    var l = lines[i];
                    int titleH, descH, x = Px(18), w = TextWidth(l.Control);
                    int y = l.Top + (l.Height - TextHeight(l, tf, df, out titleH, out descH)) / 2;
                    Ui.Text(g, l.Title, tf, Theme.Ink, new Rectangle(x, y, w, titleH), TextFormatFlags.WordBreak);
                    if (l.Desc != null) Ui.Text(g, l.Desc, df, Theme.Dim, new Rectangle(x, y + titleH + Px(3), w, descH), TextFormatFlags.WordBreak);
                    if (i > 0) g.DrawLine(rule, Px(18), l.Top + 0.5f, Width - Px(18), l.Top + 0.5f);
                }
        }
    }
}
