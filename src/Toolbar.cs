using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;

namespace PhanTrang
{
    /// <summary>One button on the toolbar or in the row of choices above it.</summary>
    public sealed class ToolButton
    {
        /// <summary>Reported when clicked ("more:" + Id for the ⌄ part of a split button or a menu button).</summary>
        public string Id;
        /// <summary>A <see cref="ToolIcons"/> name; Label is the text under (or beside) it.</summary>
        public string Icon, Label;
        /// <summary>A short line shown above the button while the mouse rests on it.</summary>
        public string Tip;
        public bool On, Off;
        /// <summary>A thin vertical line between groups of buttons.</summary>
        public bool Divider;
        /// <summary>Has a separate ⌄ part on the right that opens more choices.</summary>
        public bool Split;
        /// <summary>The whole button opens the choices.</summary>
        public bool OpensMenu;
        /// <summary>Icon and text side by side (mode tabs, the board chip) instead of text under the icon.</summary>
        public bool Inline;
        public bool IconOnly;
        /// <summary>Neighbouring buttons with the same group share a background: "modes" (tinted) or "board" (green chip).</summary>
        public string Group;
        /// <summary>Width at 100 % scale when not the standard one.</summary>
        public int Width;

        public static ToolButton Line() { return new ToolButton { Divider = true }; }
    }

    /// <summary>
    /// The cream bar at the bottom of the screen, with an optional row of choices above it and a faint notice
    /// above that. Drawn into its own layered window; clicks land on it directly (it is not see-through).
    /// </summary>
    public sealed class Toolbar
    {
        public Func<List<ToolButton>> MainButtons;
        public Func<string, List<ToolButton>> ChoiceButtons;
        public Action<string> Pressed;

        /// <summary>A laid-out row: its buttons, where each one is, and the tray around them.</summary>
        sealed class Row
        {
            public List<ToolButton> Buttons = new List<ToolButton>();
            public readonly List<Rectangle> Boxes = new List<Rectangle>();
            public Rectangle Tray;
            public bool IsChoices;
        }

        /// <summary>A place on the toolbar: a button of a row, optionally its ⌄ part.</summary>
        sealed class Spot
        {
            public Row Row;
            public int Index;
            public bool Chevron;
            public ToolButton Button { get { return Row.Buttons[Index]; } }
            public Rectangle Box { get { return Row.Boxes[Index]; } }
            public bool Same(Spot o) { return o != null && o.Row == Row && o.Index == Index && o.Chevron == Chevron; }
        }

        /// <summary>A notice that fades in after 'wait', stays, and fades out.</summary>
        sealed class Notice
        {
            readonly double wait, fadeIn, hold, fadeOut;
            double age = -1;
            public float Alpha;
            public Notice(double wait, double fadeIn, double hold, double fadeOut) { this.wait = wait; this.fadeIn = fadeIn; this.hold = hold; this.fadeOut = fadeOut; }
            public bool Running { get { return age >= 0; } }
            public void Start() { age = 0; Alpha = 0; }
            public void Stop() { age = -1; Alpha = 0; }

            /// <summary>Advances; true if the alpha changed enough to redraw.</summary>
            public bool Advance(double dt)
            {
                if (age < 0) return false;
                age += dt;
                double t = age - wait;
                float a = t <= 0 ? 0 : t < fadeIn ? (float)(t / fadeIn) : t < fadeIn + hold ? 1 : (float)Math.Max(0, 1 - (t - fadeIn - hold) / fadeOut);
                if (t > fadeIn + hold + fadeOut) { age = -1; a = 0; }
                bool changed = Math.Abs(a - Alpha) > 0.004f || (a == 0 && Alpha != 0);
                Alpha = a;
                return changed;
            }
        }

        static readonly Color ModeTint = Color.FromArgb(239, 233, 218);
        static readonly StringFormat Typo = TypoFormat();
        static StringFormat TypoFormat() { var f = (StringFormat)StringFormat.GenericTypographic.Clone(); f.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces; return f; }

        readonly GlassWindow glass = new GlassWindow(false);
        PixelBuffer buf;
        RECT work;
        double dpiScale = 1, k = 1;              // k = dpiScale, shrunk if the bar would not fit the screen
        readonly Row main = new Row(), choices = new Row { IsChoices = true };
        string openChoices;
        Spot hover;
        Spot tipFor;
        readonly Notice tip = new Notice(0.35, 0.15, 1.1, 0.45);
        readonly Notice coach = new Notice(0, 0.2, 2.6, 0.6);
        string[][] coachText;
        bool active, onScreen;
        double opacity, wantOpacity;

        // sizes in pixels at the current scale
        int btnW, btnH, spacing, inset, dividerW, trayH, rowGap, noticeH, chevronW;

        public Toolbar()
        {
            glass.Received += m =>
            {
                if (m.Msg != Win32.Wm.LButtonUp) return;
                long lp = m.LParam.ToInt64();
                Click((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
            };
        }

        int Px(double v) { return (int)Math.Round(v * k); }
        bool Labels { get { return Settings.ToolbarLabels; } }
        int Left { get { return work.Left + (work.Width - buf.W) / 2; } }
        int Top { get { return work.Bottom - buf.H - (int)(10 * k); } }

        public bool ChoicesOpen { get { return openChoices != null; } }
        public string OpenChoices { get { return openChoices; } }
        public bool Revealed { get { return wantOpacity > 0; } }

        RECT OnScreen(Rectangle r) { return new RECT(Left + r.Left, Top + r.Top, Left + r.Right, Top + r.Bottom); }

        /// <summary>The parts of the screen that belong to the toolbar (clicks there are not captured).</summary>
        public List<RECT> ScreenRects
        {
            get
            {
                var list = new List<RECT>();
                if (buf == null || !active || opacity < 0.05) return list;
                list.Add(OnScreen(main.Tray));
                if (openChoices != null) list.Add(OnScreen(choices.Tray));
                return list;
            }
        }

        /// <summary>The highest point of the toolbar on screen, for placing a window just above it.</summary>
        public int ScreenTop { get { return buf == null ? work.Bottom : Top + (openChoices != null ? choices.Tray.Top : main.Tray.Top); } }

        /// <summary>Whether the mouse is close enough that a hidden toolbar should come back.</summary>
        public bool Near(int x, int y)
        {
            if (buf == null) return false;
            var top = openChoices != null ? choices.Tray.Top : main.Tray.Top;
            return x >= Left + main.Tray.Left - Px(80) && x < Left + main.Tray.Right + Px(80)
                && y >= Top + top - Px(90) && y < work.Bottom + 4;
        }

        public void CloseChoices() { if (openChoices != null) { openChoices = null; Rebuild(); } }
        public void ToggleChoices(string id) { openChoices = openChoices == id ? null : id; Rebuild(); }

        /// <summary>A one-line tip after switching tools: pairs of (bold, plain) text.</summary>
        public void Coach(string[][] parts) { coachText = parts; coach.Start(); Rebuild(); }

        public void Place(RECT workArea, double scale)
        {
            if (buf != null && workArea.SameAs(work) && Math.Abs(scale - dpiScale) < 0.01) return;
            work = workArea;
            dpiScale = scale;
            Rebuild();
        }

        // ---- layout ----

        void Measure()
        {
            btnW = Px(Labels ? 64 : 46);
            btnH = Px(Labels ? 56 : 46);
            spacing = Px(2);
            inset = Px(8);
            dividerW = Px(13);
            rowGap = Px(10);
            noticeH = Px(34);
            chevronW = Px(18);
            trayH = btnH + 2 * inset;
        }

        Font LabelFont() { return Fonts.Get("uiSemi", (float)Math.Max(11, Math.Round(12 * k))); }
        Font InlineFont() { return Fonts.Get("uiSemi", (float)Math.Round(14 * k)); }
        Font NoticeFont(bool bold) { return Fonts.Get(bold ? "uiSemi" : "ui", (float)Math.Round(13 * k)); }

        /// <summary>The ⌄ part; on the board chip it is wider, so it reads as its own button.</summary>
        int ChevronW(ToolButton b) { return b.Group == "board" ? Px(34) : chevronW; }

        int WidthOf(ToolButton b, Graphics measure, Font inline)
        {
            if (b.Divider) return dividerW;
            if (b.Width > 0) return Px(b.Width);
            int extra = b.Split ? ChevronW(b) : 0;
            if (b.Inline)
            {
                float text = measure.MeasureString(b.Label ?? "", inline, PointF.Empty, StringFormat.GenericTypographic).Width;
                return (int)Math.Ceiling(Px(12) + Px(22) + Px(8) + text + Px(14)) + extra;
            }
            return b.IconOnly ? Px(44) : btnW + extra;
        }

        List<int> Widths(Row row, Graphics measure, Font inline) { return row.Buttons.Select(b => WidthOf(b, measure, inline)).ToList(); }

        int RowWidth(List<int> widths) { return widths.Count == 0 ? 0 : 2 * inset + widths.Sum() + spacing * (widths.Count - 1); }

        /// <summary>Lays the buttons of 'row' left to right inside a tray at (x, y).</summary>
        void Lay(Row row, List<int> widths, int x, int y)
        {
            row.Tray = new Rectangle(x, y, RowWidth(widths), trayH);
            row.Boxes.Clear();
            int at = x + inset;
            foreach (int w in widths) { row.Boxes.Add(new Rectangle(at, y + inset, w, btnH)); at += w + spacing; }
        }

        /// <summary>Asks for the buttons again and lays everything out (smaller if the screen is narrow).</summary>
        public void Rebuild()
        {
            if (MainButtons == null) return;
            main.Buttons = MainButtons();
            choices.Buttons = openChoices != null && ChoiceButtons != null ? ChoiceButtons(openChoices) : new List<ToolButton>();
            if (choices.Buttons.Count == 0) openChoices = null;

            List<int> mainW, choiceW;
            int noticeW = 0;
            using (var scratch = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(scratch))
            {
                k = dpiScale;
                for (int attempt = 0; ; attempt++)
                {
                    Measure();
                    using (var f = InlineFont()) { mainW = Widths(main, g, f); choiceW = Widths(choices, g, f); }
                    int room = Math.Max(200, work.Width - (int)(24 * dpiScale)), need = RowWidth(mainW);
                    if (need <= room || attempt == 1) break;
                    k = dpiScale * room / need;   // one more pass at the scale that fits
                }
                if (coachText != null) noticeW = (int)Math.Ceiling(CoachWidth(g));
            }

            int W = Math.Max(Math.Max(RowWidth(mainW), RowWidth(choiceW)), noticeW) + 8;
            int H = 2 * trayH + 2 * rowGap + noticeH + 8;
            if (buf == null || buf.W != W || buf.H != H)
            {
                if (buf != null) buf.Dispose();
                buf = new PixelBuffer(W, H);
            }

            Lay(main, mainW, (W - RowWidth(mainW)) / 2, H - trayH - 4);
            choices.Boxes.Clear();
            choices.Tray = Rectangle.Empty;
            if (choices.Buttons.Count > 0)
            {
                // the choices open above the button they belong to (the lesson menu belongs to the board chip)
                string owner = openChoices == "lesson" ? "board" : openChoices;
                int i = main.Buttons.FindIndex(b => b.Id == owner);
                int centre = i >= 0 ? main.Boxes[i].Left + main.Boxes[i].Width / 2 : W / 2;
                int width = RowWidth(choiceW);
                Lay(choices, choiceW, Math.Max(4, Math.Min(W - width - 4, centre - width / 2)), main.Tray.Top - rowGap - trayH);
            }
            if (hover != null && (hover.Index >= hover.Row.Buttons.Count)) hover = null;
            Repaint();
        }

        // ---- painting ----

        static GraphicsPath Rounded(RectangleF r, float radius) { return Draw.Round(r, radius); }

        void PaintTray(Graphics g, Rectangle r)
        {
            float radius = Px(Ui.RadiusLarge);
            // three soft layers of shadow under the tray
            for (int layer = 1; layer <= 3; layer++)
                using (var shade = Rounded(new RectangleF(r.X - layer + 1, r.Y + layer * 1.6f * (float)k, r.Width + 2 * layer - 2, r.Height + layer - 1), radius + layer))
                using (var b = new SolidBrush(Color.FromArgb(32 - 6 * layer, 20, 30, 25))) g.FillPath(b, shade);
            using (var shape = Rounded(r, radius))
            using (var paper = new SolidBrush(Theme.Paper)) g.FillPath(paper, shape);
            using (var outline = Ui.Box(r, radius, true))
            using (var edge = new Pen(Theme.Line, 1f)) g.DrawPath(edge, outline);
        }

        void Repaint()
        {
            if (buf == null) return;
            var g = buf.G;
            g.ResetClip();
            g.CompositingMode = CompositingMode.SourceCopy;
            g.Clear(Color.Transparent);
            g.CompositingMode = CompositingMode.SourceOver;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;   // text sits on the opaque cream tray

            PaintTray(g, main.Tray);
            PaintRow(g, main);
            bool tipShown = false;
            if (choices.Buttons.Count > 0)
            {
                PaintTray(g, choices.Tray);
                PaintRow(g, choices);
            }
            tipShown = PaintTip(g);
            if (!tipShown && choices.Buttons.Count == 0 && coachText != null) PaintCoach(g);
            if (active && onScreen) Present();
        }

        /// <summary>The tinted tray behind the mode tabs and the green chip behind the board buttons.</summary>
        void PaintGroups(Graphics g, Row row)
        {
            for (int i = 0; i < row.Buttons.Count; )
            {
                string group = row.Buttons[i].Group;
                int last = i;
                while (group != null && last + 1 < row.Buttons.Count && row.Buttons[last + 1].Group == group) last++;
                if (group != null)
                    using (var shape = Rounded(Rectangle.FromLTRB(row.Boxes[i].Left - Px(4), row.Boxes[i].Top, row.Boxes[last].Right + Px(4), row.Boxes[last].Bottom), Px(Ui.RadiusMedium)))
                    using (var fill = new SolidBrush(group == "board" ? Theme.Green : ModeTint)) g.FillPath(fill, shape);
                i = last + 1;
            }
        }

        bool Hovering(Row row, int i, bool chevron) { return hover != null && hover.Row == row && hover.Index == i && hover.Chevron == chevron; }

        void PaintRow(Graphics g, Row row)
        {
            PaintGroups(g, row);
            using (var small = LabelFont())
            using (var inline = InlineFont())
                for (int i = 0; i < row.Buttons.Count; i++)
                {
                    var b = row.Buttons[i];
                    if (b.Divider) PaintDivider(g, row.Boxes[i]);
                    else PaintButton(g, b, row.Boxes[i], Hovering(row, i, false) && !b.Off, Hovering(row, i, true) && !b.Off,
                        !row.IsChoices && openChoices != null && b.Id == (openChoices == "lesson" ? "board" : openChoices), small, inline);
                }
        }

        void PaintDivider(Graphics g, Rectangle r)
        {
            float x = (float)Math.Floor(r.Left + r.Width / 2f) + 0.5f;
            using (var line = new Pen(Theme.Line, Math.Max(1, Px(1)))) g.DrawLine(line, x, r.Top + Px(12), x, r.Bottom - Px(12));
        }

        void Fill(Graphics g, RectangleF r, float radius, Color c)
        {
            using (var shape = Rounded(r, radius)) using (var b = new SolidBrush(c)) g.FillPath(b, shape);
        }

        void PaintButton(Graphics g, ToolButton b, Rectangle r, bool hot, bool hotChevron, bool open, Font small, Font inline)
        {
            bool chip = b.Group == "board";
            var chevron = b.Split ? new Rectangle(r.Right - ChevronW(b), r.Y, ChevronW(b), r.Height) : Rectangle.Empty;
            var face = b.Split ? new Rectangle(r.X, r.Y, r.Width - chevron.Width, r.Height) : r;

            // background: mode tab = green pill; board chip = white wash per part; tool = soft green tint and a bar underneath
            if (chip)
            {
                if (hot) Fill(g, Rectangle.Inflate(face, 0, -Px(6)), Px(8), Color.FromArgb(34, 255, 255, 255));
                if (b.Split && (hotChevron || open)) Fill(g, Rectangle.Inflate(chevron, -Px(2), -Px(6)), Px(8), Color.FromArgb(open ? 52 : 34, 255, 255, 255));
            }
            else if (b.Inline)
            {
                if (b.On || hot) Fill(g, Rectangle.Inflate(r, 0, -Px(6)), Px(Ui.RadiusSmall), b.On ? Theme.Green : Theme.Hover);
            }
            else
            {
                if (b.On || hot || hotChevron || open) Fill(g, r, Px(Ui.RadiusSmall), b.On ? Theme.GreenSoft : Theme.Hover);
                if (b.On) Fill(g, new RectangleF(face.X + Px(14), r.Bottom - Px(4), face.Width - Px(28), Px(3)), Px(1.5), Theme.Green);
            }

            Color ink = chip || (b.Inline && b.On) ? Theme.Chalk : b.On ? Theme.Green : Theme.Text;
            if (b.Off) ink = chip ? Color.FromArgb(110, Theme.Chalk) : Color.FromArgb(172, 165, 150);

            if (b.Split)
            {
                if (hotChevron && !chip && !b.On) Fill(g, chevron, Px(8), Color.FromArgb(40, Theme.Green));
                if ((!b.Inline || chip) && !b.OpensMenu)
                    using (var line = new Pen(chip ? Color.FromArgb(90, 255, 255, 255) : b.On ? Color.FromArgb(70, Theme.Green) : Theme.Line, 1))
                    {
                        float x = (float)Math.Floor((double)chevron.Left) + 0.5f;
                        g.DrawLine(line, x, r.Top + Px(14), x, r.Bottom - Px(14));
                    }
                PaintChevron(g, chevron, ink, open);
            }

            if (b.Inline)
            {
                int size = Px(22);
                var icon = new Rectangle(r.X + Px(12), r.Y + (r.Height - size) / 2, size, size);
                ToolIcons.Draw(g, b.Icon ?? b.Id, icon, ink, chip || b.On);
                using (var text = new SolidBrush(ink))
                using (var fmt = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString(b.Label, inline, text, new RectangleF(icon.Right + Px(8), r.Y, face.Right - icon.Right - Px(8), r.Height), fmt);
                return;
            }

            using (var centred = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            using (var text = new SolidBrush(ink))
            {
                if (b.Icon == "page")   // the page counter is text only
                {
                    using (var f = Fonts.Get("uiBold", (float)Math.Round(15 * k))) g.DrawString(b.Label, f, text, r, centred);
                    return;
                }
                bool swatch = (b.Icon ?? "").StartsWith("swatch:");
                int box = 2 * (int)Math.Round((b.IconOnly ? 12 : swatch ? 16 : 13) * k);   // even size: crisp strokes
                bool label = Labels && b.Label != null && !b.IconOnly;
                int top = label ? r.Y + Px(swatch ? 2 : 7) : r.Y + (r.Height - box) / 2;
                ToolIcons.Draw(g, b.Icon ?? b.Id, new Rectangle(face.X + (face.Width - box) / 2, top, box, box), ink, chip);
                if (label) g.DrawString(b.Label, small, text, new RectangleF(face.X - Px(2), r.Bottom - Px(22), face.Width + Px(4), Px(16)), centred);
            }
        }

        void PaintChevron(Graphics g, Rectangle r, Color c, bool up)
        {
            float x = r.X + r.Width / 2f, y = r.Y + r.Height / 2f, s = Px(3.5), d = up ? -1 : 1;
            using (var pen = Geo.RoundPen(c, Math.Max(1.6f, (float)(1.8 * k))))
                g.DrawLines(pen, new[] { new PointF(x - s, y - d * s / 2), new PointF(x, y + d * s / 2), new PointF(x + s, y - d * s / 2) });
        }

        // ---- notices: a button's name while hovering, a tip after switching tools; both fade by themselves ----

        string TipText(Spot s)
        {
            if (s == null || s.Row.IsChoices != (openChoices != null)) return null;   // bar names only while no choices are open
            var b = s.Button;
            if (s.Chevron && b.Split && !b.OpensMenu) return "Thêm lựa chọn";
            string text = b.Tip ?? (b.IconOnly || !Labels ? b.Label : null);   // without labels every button names itself
            return string.IsNullOrEmpty(text) ? null : text;
        }

        static float Width(Graphics g, string s, Font f) { return g.MeasureString(s, f, PointF.Empty, Typo).Width; }

        bool PaintTip(Graphics g)
        {
            if (tipFor == null || !tipFor.Same(hover) || tip.Alpha <= 0.01f) return false;
            string text = TipText(tipFor);
            if (text == null) return false;
            var keep = g.TextRenderingHint;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;   // no ClearType fringes on a see-through bubble
            using (var f = NoticeFont(true))
            {
                var size = g.MeasureString(text, f, PointF.Empty, Typo);
                int w = (int)Math.Ceiling(size.Width) + Px(22), h = Px(30);
                var anchor = tipFor.Box;
                int x = Math.Max(2, Math.Min(buf.W - w - 2, anchor.Left + anchor.Width / 2 - w / 2));
                var bubble = new Rectangle(x, Math.Max(0, tipFor.Row.Tray.Top - Px(8) - h), w, h);
                Fill(g, bubble, h / 2f, Color.FromArgb((int)(200 * tip.Alpha), Theme.Ink));
                using (var white = new SolidBrush(Color.FromArgb((int)(255 * tip.Alpha), Color.White)))
                    g.DrawString(text, f, white, bubble.X + Px(11), (float)Math.Round(bubble.Y + (h - size.Height) / 2f), Typo);
            }
            g.TextRenderingHint = keep;
            return true;
        }

        float CoachWidth(Graphics g)
        {
            using (var bold = NoticeFont(true))
            using (var plain = NoticeFont(false))
                return coachText.Sum(p => Width(g, p[0] + " ", bold) + Width(g, p[1], plain)) + Px(26) * (coachText.Length - 1) + Px(36);
        }

        void PaintCoach(Graphics g)
        {
            if (coach.Alpha <= 0.01f) return;
            var keep = g.TextRenderingHint;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            int a = (int)(255 * coach.Alpha), h = Px(32);
            using (var bold = NoticeFont(true))
            using (var plain = NoticeFont(false))
            using (var yellow = new SolidBrush(Color.FromArgb(a, Theme.Yellow)))
            using (var white = new SolidBrush(Color.FromArgb(a, Color.White)))
            using (var dot = new SolidBrush(Color.FromArgb(a / 2, Color.White)))
            {
                float w = CoachWidth(g);
                var pill = new Rectangle((int)Math.Round((buf.W - w) / 2f), main.Tray.Top - Px(8) - h, (int)Math.Ceiling(w), h);
                Fill(g, pill, h / 2f, Color.FromArgb((int)(190 * coach.Alpha), Theme.Ink));
                float x = pill.Left + Px(18), y = (float)Math.Round(pill.Top + (h - bold.GetHeight(g)) / 2f);
                for (int i = 0; i < coachText.Length; i++)
                {
                    if (i > 0) { g.DrawString("·", plain, dot, x + Px(10), y, Typo); x += Px(26); }
                    string lead = coachText[i][0] + " ";
                    g.DrawString(lead, bold, yellow, x, y, Typo); x += Width(g, lead, bold);
                    g.DrawString(coachText[i][1], plain, white, x, y, Typo); x += Width(g, coachText[i][1], plain);
                }
            }
            g.TextRenderingHint = keep;
        }

        // ---- input ----

        Spot Find(int x, int y)
        {
            foreach (var row in new[] { choices, main })
                for (int i = 0; i < row.Boxes.Count; i++)
                {
                    var r = row.Boxes[i];
                    if (row.Buttons[i].Divider || !r.Contains(x, y)) continue;
                    var b = row.Buttons[i];
                    return new Spot { Row = row, Index = i, Chevron = b.Split && x >= r.Right - ChevronW(b) };
                }
            return null;
        }

        void Click(int x, int y)
        {
            var s = Find(x, y);
            if (s == null) return;
            var b = s.Button;
            if (b.Off || b.Icon == "page") return;
            if (s.Row.IsChoices) openChoices = null;   // picking a choice closes the row
            if (Pressed != null) Pressed(s.Chevron || b.OpensMenu ? "more:" + b.Id : b.Id);
            Rebuild();
        }

        /// <summary>Follows the mouse (screen coordinates) for hover highlights and name tips.</summary>
        public void Hover(int screenX, int screenY)
        {
            if (buf == null) return;
            var s = active ? Find(screenX - Left, screenY - Top) : null;
            if (s == null ? hover == null : s.Same(hover)) return;
            hover = s;
            // a button newly under the mouse shows its name once; resting there does not repeat it
            if (TipText(s) != null) { tipFor = s; tip.Start(); }
            else { tipFor = null; tip.Stop(); }
            Repaint();
        }

        // ---- showing and fading ----

        public void Reveal(bool show)
        {
            wantOpacity = show ? 1 : 0;
            if (!show && openChoices != null) { openChoices = null; Rebuild(); }
        }

        public void Show() { active = true; opacity = 0; wantOpacity = 1; Rebuild(); }

        public void Hide()
        {
            active = false;
            openChoices = null;
            opacity = wantOpacity = 0;
            glass.Hide();
            onScreen = false;
        }

        /// <summary>Advances the fade and the notices; true while something is still changing.</summary>
        public bool Animate(double dt)
        {
            if (!active || buf == null) return false;
            double was = opacity, step = dt / 0.16;
            opacity = opacity < wantOpacity ? Math.Min(wantOpacity, opacity + step) : Math.Max(wantOpacity, opacity - step);
            if (opacity <= 0.001)
            {
                if (onScreen) { glass.Hide(); onScreen = false; }
                return false;
            }
            if (!onScreen) { Present(); glass.Show(); onScreen = true; }
            bool notices = false;
            if (tip.Running) { if (tip.Advance(dt)) Repaint(); notices |= tip.Running; }
            if (coach.Running)
            {
                if (coach.Advance(dt)) Repaint();
                if (!coach.Running) { coachText = null; Rebuild(); }
                notices |= coach.Running;
            }
            if (opacity != was) Present();
            return opacity != wantOpacity || notices;
        }

        /// <summary>Sends the picture to the window; the fade uses the layered window's overall opacity, so nothing is redrawn.</summary>
        void Present()
        {
            if (buf == null || !active) return;
            buf.G.Flush(FlushIntention.Sync);
            Win32.GdiFlush();
            var at = new POINT(Left, Top);
            var size = new SIZE(buf.W, buf.H);
            var origin = new POINT(0, 0);
            var blend = new BLENDFUNCTION { SourceConstantAlpha = (byte)Math.Round(opacity * 255), AlphaFormat = 1 };
            Win32.UpdateLayeredWindow(glass.Handle, IntPtr.Zero, ref at, ref size, buf.Hdc, ref origin, 0, ref blend, Win32.LayeredAlpha);
        }

        public void Raise() { if (onScreen) glass.Raise(); }
    }
}
