using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace PhanTrang
{
    /// <summary>
    /// A floating card above the slide and the board ("Đếm ngược", "Gọi tên", "Chia nhóm", "Thi đua"): a title strip
    /// to drag it by, a close button, owner-drawn buttons, and a corner to resize it (up to the whole screen, so the
    /// back row can read it). The picture inside is drawn by <see cref="ClassArt"/>.
    /// </summary>
    abstract class ClassCard : Form
    {
        protected sealed class Hit
        {
            public string Id, Label, Icon;
            public RectangleF Rect;
            public bool Primary, Off, Toggle;
        }

        protected readonly float k;
        protected readonly List<Hit> Hits = new List<Hit>();
        readonly Timer ticker = new Timer { Interval = 33 };
        readonly string icon, title;
        Hit hover, pressed;
        Rectangle normalBounds;
        public bool Big { get; private set; }

        public event Action<ClassCard> Closing2;

        protected int S(double v) { return (int)Math.Round(v * k); }
        protected int TitleH { get { return S(46); } }
        protected int Pad { get { return S(14); } }
        protected static double Now { get { return Clock.Now; } }

        protected ClassCard(string icon, string title, double scale, Size size)
        {
            this.icon = icon;
            this.title = title;
            k = (float)scale;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            BackColor = Theme.Paper;
            Text = title;
            MinimumSize = new Size(S(280), S(220));
            ClientSize = new Size(S(size.Width), S(size.Height));
            ticker.Tick += (s, e) => { if (!Tick()) ticker.Stop(); Invalidate(); };
            HandleCreated += (s, e) => Ui.RoundWindow(Handle);
            Resize += (s, e) => { if (IsHandleCreated) Ui.RoundWindow(Handle); };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        /// <summary>Keeps redrawing about 30 times a second while <see cref="Tick"/> returns true.</summary>
        protected void Animate() { if (!ticker.Enabled) ticker.Start(); }
        /// <summary>Called on every animation frame; false = nothing moves any more.</summary>
        protected virtual bool Tick() { return false; }

        protected Rectangle Body { get { return Rectangle.FromLTRB(Pad, TitleH + S(4), ClientSize.Width - Pad, ClientSize.Height - Pad); } }
        RectangleF CloseRect { get { return new RectangleF(ClientSize.Width - S(44), S(7), S(34), S(32)); } }
        RectangleF BigRect { get { return new RectangleF(ClientSize.Width - S(82), S(7), S(34), S(32)); } }

        /// <summary>The subclass lays out its buttons (into <see cref="Hits"/>) and paints its picture.</summary>
        protected abstract void LayoutHits(Rectangle body);
        protected abstract void PaintBody(Graphics g, Rectangle body);
        protected abstract void Pressed(string id);

        protected Hit Add(string id, string label, RectangleF r, bool primary = false, bool off = false)
        {
            var h = new Hit { Id = id, Label = label, Rect = r, Primary = primary, Off = off };
            Hits.Add(h);
            return h;
        }

        /// <summary>A row of buttons left to right in r, widths in proportion to the given weights; returns their rects.</summary>
        protected List<RectangleF> Row(RectangleF r, params float[] weights)
        {
            float gap = S(6), total = weights.Sum(), x = r.X, room = r.Width - gap * (weights.Length - 1);
            var list = new List<RectangleF>();
            foreach (var w in weights) { float bw = room * w / total; list.Add(new RectangleF(x, r.Y, bw, r.Height)); x += bw + gap; }
            return list;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Theme.Paper);
            using (var pen = new Pen(Theme.Line, 1)) g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);

            // title strip
            using (var b = new SolidBrush(Theme.Green)) g.FillRectangle(b, 0, 0, ClientSize.Width, TitleH);
            int iconSize = S(26);
            if (!ClassArt.Icon(g, icon, new Rectangle(S(12), (TitleH - iconSize) / 2, iconSize, iconSize), Theme.Chalk, true))
                ToolIcons.Draw(g, icon, new Rectangle(S(12), (TitleH - iconSize) / 2, iconSize, iconSize), Theme.Chalk, true);
            using (var f = Fonts.Get("uiBold", 16 * k))
                Ui.Text(g, title, f, Theme.Chalk, new Rectangle(S(46), 0, ClientSize.Width - S(140), TitleH), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            PaintTitleButton(g, BigRect, Big ? "shrink" : "grow", hover != null && hover.Id == "#big");
            PaintTitleButton(g, CloseRect, "close", hover != null && hover.Id == "#close");

            var body = Body;
            Hits.Clear();
            LayoutHits(body);
            PaintBody(g, body);
            foreach (var h in Hits) if (h.Label != null) PaintButton(g, h);

            // the corner to resize by
            using (var pen = new Pen(Theme.LineStrong, Math.Max(1, k * 1.5f)))
                for (int i = 1; i <= 3; i++) g.DrawLine(pen, ClientSize.Width - S(4) - S(4 * i), ClientSize.Height - S(4), ClientSize.Width - S(4), ClientSize.Height - S(4) - S(4 * i));
        }

        /// <summary>Close (✕), and grow / shrink: four corner marks pointing out or in.</summary>
        void PaintTitleButton(Graphics g, RectangleF r, string kind, bool hot)
        {
            if (hot) using (var p = Draw.Round(r, S(8))) using (var b = new SolidBrush(Color.FromArgb(50, 255, 255, 255))) g.FillPath(b, p);
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, s = S(7);
            using (var pen = new Pen(Theme.Chalk, Math.Max(1.6f, 1.8f * k)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (kind == "close") { g.DrawLine(pen, cx - s * 0.8f, cy - s * 0.8f, cx + s * 0.8f, cy + s * 0.8f); g.DrawLine(pen, cx + s * 0.8f, cy - s * 0.8f, cx - s * 0.8f, cy + s * 0.8f); return; }
                float a = kind == "grow" ? s : s * 0.35f;                               // how far the corner sits from the middle
                float arm = s * 0.55f * (kind == "grow" ? -1 : 1);                      // arms point back in (grow) or out (shrink)
                foreach (var d in new[] { new PointF(-1, -1), new PointF(1, -1), new PointF(1, 1), new PointF(-1, 1) })
                {
                    var tip = new PointF(cx + d.X * a, cy + d.Y * a);
                    g.DrawLines(pen, new[] { new PointF(tip.X + d.X * arm, tip.Y), tip, new PointF(tip.X, tip.Y + d.Y * arm) });
                }
            }
        }

        protected void PaintButton(Graphics g, Hit h)
        {
            bool hot = hover == h && !h.Off, down = pressed == h && hot;
            Color fill = h.Off ? Theme.Tray : h.Primary || h.Toggle ? (hot ? Color.FromArgb(44, 98, 78) : Theme.Green) : hot ? Theme.Hover : Color.White;
            Color ink = h.Off ? Theme.Dim : h.Primary || h.Toggle ? Theme.Chalk : Theme.Text;
            var r = down ? RectangleF.Inflate(h.Rect, -1, -1) : h.Rect;
            using (var p = Draw.Round(r, Math.Min(S(10), r.Height / 2)))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, p);
                if (!h.Primary && !h.Toggle) using (var pen = new Pen(Theme.Line, 1)) g.DrawPath(pen, p);
            }
            float px = Math.Max(11 * k, Math.Min(r.Height * 0.42f, 20 * k));
            using (var f = Fonts.Get(h.Primary ? "uiBold" : "uiSemi", px))
                Ui.Text(g, h.Label, f, ink, Rectangle.Round(r), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        Hit HitAt(Point p)
        {
            if (CloseRect.Contains(p)) return new Hit { Id = "#close" };
            if (BigRect.Contains(p)) return new Hit { Id = "#big" };
            for (int i = Hits.Count - 1; i >= 0; i--) if (Hits[i].Rect.Contains(p)) return Hits[i];
            return null;
        }

        static bool Same(Hit a, Hit b) { return a == b || (a != null && b != null && a.Id == b.Id && a.Rect == b.Rect); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var h = HitAt(e.Location);
            if (Same(h, hover)) return;
            hover = h;
            Cursor = h != null && !h.Off ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e) { hover = null; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = HitAt(e.Location); Invalidate(); } }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            var h = HitAt(e.Location);
            bool same = Same(h, pressed);
            pressed = null;
            Invalidate();
            if (e.Button != MouseButtons.Left || h == null || !same || h.Off) return;
            if (h.Id == "#close") { Close(); return; }
            if (h.Id == "#big") { ToggleBig(); return; }
            Pressed(h.Id);
        }

        /// <summary>Whole work area (for the back row) and back.</summary>
        public void ToggleBig()
        {
            if (!Big)
            {
                normalBounds = Bounds;
                Bounds = Screen.FromControl(this).WorkingArea;
            }
            else Bounds = normalBounds;
            Big = !Big;
            Invalidate();
        }

        const int WmNcHitTest = 0x84, HtClient = 1, HtCaption = 2, HtBottomRight = 17;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmNcHitTest)
            {
                base.WndProc(ref m);
                if (m.Result.ToInt64() != HtClient) return;
                long lp = m.LParam.ToInt64();
                var p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
                if (p.X >= ClientSize.Width - S(22) && p.Y >= ClientSize.Height - S(22)) m.Result = (IntPtr)HtBottomRight;
                else if (p.Y < TitleH && !CloseRect.Contains(p) && !BigRect.Contains(p)) m.Result = (IntPtr)HtCaption;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ticker.Stop();
            if (Closing2 != null) Closing2(this);
            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing) { if (disposing) ticker.Dispose(); base.Dispose(disposing); }

        /// <summary>Text drawn centred in r (for small notes inside a card).</summary>
        protected void Note(Graphics g, string text, RectangleF r, float px, Color c)
        {
            using (var f = Fonts.Get("uiSemi", px))
                Ui.Text(g, text, f, c, Rectangle.Round(r), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    // ---------------- Đếm ngược ----------------

    sealed class TimerCard : ClassCard
    {
        readonly Countdown clock = new Countdown(Settings.TimerSeconds);
        readonly Sounds sounds;
        bool rang;
        static readonly int[] Presets = { 30, 60, 120, 180, 300, 600, 900 };

        public TimerCard(double scale, Sounds sounds) : base("timer", "Đếm ngược", scale, new Size(420, 430)) { this.sounds = sounds; }

        protected override void LayoutHits(Rectangle body)
        {
            float h = S(40), y2 = body.Bottom - h, y1 = y2 - h - S(8), y0 = y1 - h - S(8);
            var presets = Row(new RectangleF(body.X, y0, body.Width, h), 1, 1, 1, 1, 1, 1, 1);
            for (int i = 0; i < Presets.Length; i++)
            {
                int s = Presets[i];
                var b = Add("set:" + s, s < 60 ? s + "s" : s / 60 + "′", presets[i]);
                b.Toggle = Math.Abs(clock.Total - s) < 0.5 && !clock.Running;
            }
            var adjust = Row(new RectangleF(body.X, y1, body.Width, h), 1, 1, 1, 1);
            Add("add:-60", "− 1 phút", adjust[0]);
            Add("add:-10", "− 10s", adjust[1]);
            Add("add:10", "+ 10s", adjust[2]);
            Add("add:60", "+ 1 phút", adjust[3]);
            var main = Row(new RectangleF(body.X, y2, body.Width, h), 2, 1);
            bool ended = clock.Ended(Now);
            Add(clock.Running ? "pause" : "start", clock.Running ? "Tạm dừng" : ended ? "Đếm lại" : "Bắt đầu", main[0], true);
            Add("reset", "Đặt lại", main[1]);
        }

        protected override void PaintBody(Graphics g, Rectangle body)
        {
            float bottom = Hits.Count > 0 ? Hits.Min(h => h.Rect.Top) - S(10) : body.Bottom;
            var face = RectangleF.FromLTRB(body.X, body.Y + S(4), body.Right, bottom);
            double now = Now, left = clock.Remaining(now);
            ClassArt.PaintTimer(g, face, left, clock.Total, clock.Running, left <= 0, now);
        }

        protected override bool Tick()
        {
            double now = Now;
            if (clock.Running && clock.Ended(now))
            {
                clock.Pause(now);
                if (!rang) { rang = true; sounds.Bell(); }
                endedAt = now;
            }
            return clock.Running || now - endedAt < 6;   // a few seconds of the "Hết giờ!" animation
        }

        double endedAt = -100;

        protected override void Pressed(string id)
        {
            double now = Now;
            int colon = id.IndexOf(':');
            string kind = colon < 0 ? id : id.Substring(0, colon);
            int arg = colon < 0 ? 0 : int.Parse(id.Substring(colon + 1));
            switch (kind)
            {
                case "set": clock.Set(arg); Settings.TimerSeconds = arg; rang = false; break;
                case "add": clock.AddTime(arg, now); if (clock.Remaining(now) > 0) rang = false; break;
                case "start":
                    if (clock.Ended(now)) clock.Reset();
                    rang = false;
                    clock.Start(now);
                    break;
                case "pause": clock.Pause(now); break;
                case "reset": clock.Reset(); rang = false; break;
            }
            Animate();
            Invalidate();
        }
    }

    // ---------------- Gọi tên ----------------

    sealed class PickerCard : ClassCard
    {
        NamePicker picker;
        readonly Sounds sounds;
        readonly Func<bool> editList;
        string name;
        List<string> reel = new List<string>();
        double spinStart = -1;
        const double SpinTime = 2.2;

        public PickerCard(double scale, Sounds sounds, Func<bool> editList) : base("picker", "Gọi tên", scale, new Size(460, 400))
        {
            this.sounds = sounds;
            this.editList = editList;
            Reload();
        }

        /// <summary>Takes the class list again (after it was edited).</summary>
        public void Reload() { picker = new NamePicker(ClassRoster.Load()); name = null; reel.Clear(); Invalidate(); }

        double Spin { get { return spinStart < 0 ? 1 : Math.Min(1, (Now - spinStart) / SpinTime); } }

        protected override void LayoutHits(Rectangle body)
        {
            float h = S(42), y = body.Bottom - h;
            var row = Row(new RectangleF(body.X, y, body.Width, h), 2, 1.4f, 1.2f);
            bool empty = picker.Count == 0;
            Add("spin", empty ? "Nhập danh sách" : "Quay", row[0], true, Spin < 1);
            Add("list", "Danh sách…", row[1]);
            Add("again", "Gọi lại", row[2], false, empty || picker.Called.Count == 0);
        }

        protected override void PaintBody(Graphics g, Rectangle body)
        {
            float bottom = Hits.Min(x => x.Rect.Top) - S(8), noteH = S(24);
            var card = RectangleF.FromLTRB(body.X, body.Y + S(4), body.Right, bottom - noteH);
            ClassArt.PaintNameCard(g, card, picker.Count == 0 ? null : name, Spin, reel, Now);
            string note = picker.Count == 0 ? "Chưa có danh sách lớp" : "Còn " + picker.Left + " / " + picker.Count + " bạn chưa được gọi";
            Note(g, note, new RectangleF(body.X, bottom - noteH, body.Width, noteH), 13 * k, Theme.Dim);
        }

        protected override bool Tick()
        {
            if (spinStart < 0) return false;
            if (Now - spinStart >= SpinTime) { spinStart = -1; sounds.Ting(); return true; }
            return true;
        }

        protected override void Pressed(string id)
        {
            switch (id)
            {
                case "spin":
                    if (picker.Count == 0) { if (editList()) Reload(); return; }
                    name = picker.Next();
                    reel = picker.Reel(18);
                    reel.Add(name);
                    spinStart = Now;
                    Animate();
                    break;
                case "list": if (editList()) Reload(); break;
                case "again": picker.Reset(); name = null; break;
            }
            Invalidate();
        }
    }

    // ---------------- Chia nhóm ----------------

    sealed class GroupsCard : ClassCard
    {
        readonly Func<bool> editList;
        readonly Action<IList<string>> toContest;
        readonly Random rng = new Random();
        List<List<string>> groups = new List<List<string>>();
        int count = Math.Max(2, Math.Min(12, Settings.GroupCount));
        double shuffledAt = -10;

        public GroupsCard(double scale, Func<bool> editList, Action<IList<string>> toContest) : base("groups", "Chia nhóm", scale, new Size(720, 520))
        {
            this.editList = editList;
            this.toContest = toContest;
        }

        protected override void LayoutHits(Rectangle body)
        {
            float h = S(42), y = body.Bottom - h;
            var row = Row(new RectangleF(body.X, y, body.Width, h), 0.7f, 1.1f, 0.7f, 1.8f, 1.3f, 1.4f);
            Add("less", "−", row[0], false, count <= 2);
            Add(null, count + " nhóm", row[1]).Off = true;
            Add("more", "+", row[2], false, count >= 12);
            Add("split", groups.Count == 0 ? "Chia nhóm" : "Chia lại", row[3], true);
            Add("list", "Danh sách…", row[4]);
            Add("contest", "→ Thi đua", row[5], false, groups.Count == 0);
        }

        protected override void PaintBody(Graphics g, Rectangle body)
        {
            float bottom = Hits.Min(x => x.Rect.Top) - S(10);
            var area = RectangleF.FromLTRB(body.X, body.Y + S(4), body.Right, bottom);
            if (groups.Count == 0)
            {
                int n = ClassRoster.Load().Count;
                Note(g, n == 0 ? "Bấm “Danh sách…” để nhập tên học sinh" : n + " học sinh · chọn số nhóm rồi bấm Chia nhóm", area, 15 * k, Theme.Dim);
                return;
            }
            // the grid that gives each card the most room (cards a little wider than tall read best)
            int cols = 1;
            float best = -1;
            for (int c = 1; c <= groups.Count; c++)
            {
                int rws = (int)Math.Ceiling(groups.Count / (double)c);
                float cw = (area.Width - S(10) * (c - 1)) / c, chh = (area.Height - S(10) * (rws - 1)) / rws;
                float score = Math.Min(cw, chh * 1.4f);
                if (score > best) { best = score; cols = c; }
            }
            int rows = (int)Math.Ceiling(groups.Count / (double)cols);
            float gap = S(10), w = (area.Width - gap * (cols - 1)) / cols, ch = (area.Height - gap * (rows - 1)) / rows;
            double t = Now - shuffledAt;
            for (int i = 0; i < groups.Count; i++)
            {
                var r = new RectangleF(area.X + (i % cols) * (w + gap), area.Y + (i / cols) * (ch + gap), w, ch);
                // cards drop in one after another
                double appear = Math.Max(0, Math.Min(1, (t - i * 0.07) / 0.25));
                if (appear <= 0) continue;
                var st = g.Save();
                float lift = (float)((1 - appear) * S(24));
                g.TranslateTransform(0, -lift);
                ClassArt.PaintGroupCard(g, r, i, "Nhóm " + (i + 1), groups[i]);
                g.Restore(st);
            }
        }

        protected override bool Tick() { return Now - shuffledAt < groups.Count * 0.07 + 0.3; }

        protected override void Pressed(string id)
        {
            switch (id)
            {
                case "less": count = Math.Max(2, count - 1); Settings.GroupCount = count; break;
                case "more": count = Math.Min(12, count + 1); Settings.GroupCount = count; break;
                case "split":
                    var names = ClassRoster.Load();
                    if (names.Count == 0) { if (!editList()) return; names = ClassRoster.Load(); if (names.Count == 0) return; }
                    groups = Grouping.IntoGroups(names, count, rng);
                    shuffledAt = Now;
                    Animate();
                    break;
                case "list": editList(); groups.Clear(); break;
                case "contest": toContest(Enumerable.Range(1, groups.Count).Select(i => "Nhóm " + i).ToList()); break;
            }
            Invalidate();
        }
    }

    // ---------------- Thi đua ----------------

    sealed class ContestCard : ClassCard
    {
        Contest contest;
        readonly Sounds sounds;
        int highlight = -1;
        double changedAt = -10;

        public ContestCard(double scale, Sounds sounds, IList<string> teams) : base("contest", "Thi đua", scale, new Size(720, 460))
        {
            this.sounds = sounds;
            contest = teams != null && teams.Count >= 2 ? new Contest(teams) : Contest.Numbered(Settings.TeamCount, "Tổ");
        }

        public void UseTeams(IList<string> teams) { contest = new Contest(teams); highlight = -1; Invalidate(); }

        RectangleF Board(Rectangle body)
        {
            float bottom = body.Bottom - S(42) - S(10);
            return RectangleF.FromLTRB(body.X, body.Y + S(4), body.Right, bottom);
        }

        protected override void LayoutHits(Rectangle body)
        {
            var board = Board(body);
            int n = contest.Teams.Count;
            for (int i = 0; i < n; i++)
            {
                Add("plus:" + i, null, ClassArt.ScorePlusRect(board, n, i));
                Add("minus:" + i, null, ClassArt.ScoreMinusRect(board, n, i));
            }
            float h = S(42);
            var row = Row(new RectangleF(body.X, body.Bottom - h, body.Width, h), 0.7f, 1.1f, 0.7f, 1.3f, 1.3f);
            Add("less", "−", row[0], false, n <= 2);
            Add(null, n + " đội", row[1]).Off = true;
            Add("more", "+", row[2], false, n >= Contest.MaxTeams);
            Add("undo", "Hoàn tác", row[3], false, !contest.CanUndo);
            Add("zero", "Về 0 hết", row[4], false, contest.Scores.All(s => s == 0));
        }

        protected override void PaintBody(Graphics g, Rectangle body)
        {
            ClassArt.PaintScoreboard(g, Board(body), contest.Teams, contest.Scores, highlight, changedAt, Now);
        }

        protected override bool Tick() { return Now - changedAt < 1.2; }

        protected override void Pressed(string id)
        {
            int colon = id.IndexOf(':');
            string kind = colon < 0 ? id : id.Substring(0, colon);
            int arg = colon < 0 ? -1 : int.Parse(id.Substring(colon + 1));
            switch (kind)
            {
                case "plus": contest.Add(arg, 1); Mark(arg); sounds.Tap(); break;
                case "minus": contest.Add(arg, -1); Mark(arg); break;
                case "undo": Mark(contest.Undo()); break;
                case "zero": contest.ResetScores(); highlight = -1; break;
                case "less":
                case "more":
                    int n = contest.Teams.Count + (kind == "more" ? 1 : -1);
                    Settings.TeamCount = n;
                    contest = Contest.Numbered(n, "Tổ");
                    highlight = -1;
                    break;
            }
            Invalidate();
        }

        void Mark(int team) { highlight = team; changedAt = Now; Animate(); }
    }

    // ---------------- the class list ----------------

    /// <summary>Type or paste the names (one per line), or read them from a .txt, .csv or Excel file.</summary>
    sealed class RosterWindow : Form
    {
        readonly TextBox box = new TextBox();
        readonly Label count = new Label();

        public RosterWindow(float k)
        {
            Text = "Danh sách lớp";
            AutoScaleMode = AutoScaleMode.None;
            Font = Ui.Body(k);
            BackColor = Theme.Cream;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = MaximizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size((int)(460 * k), (int)(560 * k));
            int pad = (int)(14 * k);

            var help = new Label { Text = "Mỗi dòng một tên. Dán từ Excel, Word hay Zalo đều được; số thứ tự tự bỏ đi.\nDanh sách chỉ lưu trên máy này.", AutoSize = false, Dock = DockStyle.Top, Height = (int)(52 * k), Padding = new Padding(pad, (int)(8 * k), pad, 0), ForeColor = Theme.Text };
            box.Multiline = true;
            box.ScrollBars = ScrollBars.Vertical;
            box.AcceptsReturn = true;
            box.WordWrap = false;
            box.Font = Fonts.Get("ui", 16 * k);
            box.Dock = DockStyle.Fill;
            box.Text = string.Join("\r\n", ClassRoster.Load());
            box.TextChanged += (s, e) => Count();

            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = (int)(58 * k), Padding = new Padding(pad, (int)(10 * k), pad, 0), FlowDirection = FlowDirection.LeftToRight, BackColor = Theme.Cream };
            var open = new RoundButton("Mở tệp txt / Excel…", RoundButton.Kind.Secondary, k) { Size = new Size((int)(170 * k), (int)(40 * k)), BackColor = Theme.Cream };
            var save = new RoundButton("Lưu", RoundButton.Kind.Primary, k) { Size = new Size((int)(90 * k), (int)(40 * k)), BackColor = Theme.Cream };
            var cancel = new RoundButton("Huỷ", RoundButton.Kind.Secondary, k) { Size = new Size((int)(80 * k), (int)(40 * k)), BackColor = Theme.Cream };
            count.AutoSize = false; count.Size = new Size((int)(90 * k), (int)(40 * k)); count.TextAlign = ContentAlignment.MiddleLeft; count.ForeColor = Theme.Dim;
            open.Click += (s, e) => OpenFile();
            save.Click += (s, e) =>
            {
                try { ClassRoster.Save(Names()); DialogResult = DialogResult.OK; Close(); }
                catch (Exception ex) { Log.Write("lưu danh sách: " + ex.Message); MessageBox.Show(this, "Không lưu được danh sách.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            bar.Controls.AddRange(new Control[] { open, count, save, cancel });

            var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(pad, (int)(4 * k), pad, 0), BackColor = Theme.Cream };
            host.Controls.Add(box);
            Controls.Add(host);
            Controls.Add(bar);
            Controls.Add(help);
            Count();
        }

        List<string> Names() { return ClassRoster.Clean(box.Lines); }
        void Count() { count.Text = Names().Count + " tên"; }

        void OpenFile()
        {
            using (var d = new OpenFileDialog { Filter = "Danh sách lớp (*.txt; *.csv; *.xlsx)|*.txt;*.csv;*.xlsx;*.xlsm|Tất cả tệp (*.*)|*.*", Title = "Mở danh sách lớp" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var names = ClassRoster.FromFile(d.FileName);
                    box.Text = string.Join("\r\n", names);
                }
                catch (Exception ex)
                {
                    Log.Write("mở danh sách: " + ex.Message);
                    string why = ex is System.IO.InvalidDataException ? ex.Message : ex is System.IO.IOException ? "Tệp đang mở ở chương trình khác. Thầy cô đóng tệp rồi thử lại." : "Không đọc được tệp này.";
                    MessageBox.Show(this, why, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
