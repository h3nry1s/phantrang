using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace PhanTrang
{
    /// <summary>Collects the parts of the canvas that changed since the last frame.</summary>
    sealed class DirtyArea
    {
        Rectangle area = Rectangle.Empty;

        public bool IsEmpty { get { return area.IsEmpty; } }

        public void Add(RectangleF r)
        {
            if (!(r.Width > 0 && r.Height > 0)) return;
            var whole = Rectangle.FromLTRB((int)Math.Floor(r.Left), (int)Math.Floor(r.Top), (int)Math.Ceiling(r.Right), (int)Math.Ceiling(r.Bottom));
            area = area.IsEmpty ? whole : Rectangle.Union(area, whole);
        }

        public void Add(Rectangle r) { if (!r.IsEmpty) area = area.IsEmpty ? r : Rectangle.Union(area, r); }

        public Rectangle Take() { var r = area; area = Rectangle.Empty; return r; }
    }

    /// <summary>
    /// How the teacher's pointer looks right now: which pointer, how big, whether it is pressed against the board
    /// (it sinks onto its shadow), the ring left by a tap, and the eraser circle. Sizes follow RefH, the height of
    /// the real screen, so a small preview still shows the pointer life-size.
    /// </summary>
    public sealed class PointerLook
    {
        public const double TapSeconds = 0.45;
        const double PressRelax = 0.042;          // seconds for the press to fall back by ~63 %

        public string Kind = "chalk";
        public double Size = 0.13;                // chalk length as a fraction of RefH
        public bool Shadow = true;
        public bool Hidden;
        public float EraserRadius;
        public double RefH = 1080;
        public bool Held;

        double press;                             // 1 = pushed against the board, 0 = resting
        double tapStart = -10;
        PointF tapAt;

        public void Press() { press = 1; Held = true; }
        public void Tap(PointF at) { tapStart = Clock.Now; tapAt = at; }
        public void Reset() { press = 0; Held = false; }

        public bool Moving(double now) { return press > 0.002 || now - tapStart < TapSeconds; }

        public void Advance(double dt)
        {
            if (!Held) press *= Math.Exp(-Math.Max(0, Math.Min(dt, 0.25)) / PressRelax);
            if (press < 0.001) press = 0;
        }

        int SpriteLength { get { return (int)Math.Round(Size * RefH * 0.85 / 4) * 4; } }
        float Lift { get { return Shadow ? (float)(0.03 * RefH * (1 - 0.8 * press)) : 0; } }
        float Squash { get { return (float)(1 - 0.07 * press); } }
        float LaserRadius { get { return (float)(Math.Max(7, RefH * 0.0075 * Size / 0.13) * (1 + 0.2 * press)); } }
        double TapAge(double now) { return now - tapStart; }

        /// <summary>A short description of everything that affects the picture; when it is unchanged nothing needs redrawing.</summary>
        public string State(double x, double y, double now)
        {
            if (Hidden && TapAge(now) >= TapSeconds) return "-";
            var age = TapAge(now);
            return string.Concat(Kind, ":", (int)Math.Round(x), ",", (int)Math.Round(y), ":", (int)Math.Round(press * 50), ":",
                EraserRadius, ":", Size, Shadow ? "s" : "", Hidden ? "h" : "", age < TapSeconds ? ":" + (int)(age * 60) : "");
        }

        static Rectangle Around(float x, float y, int r) { return new Rectangle((int)x - r, (int)y - r, 2 * r, 2 * r); }

        /// <summary>The pixels the pointer (and a fresh tap ring) can touch when drawn at (x, y).</summary>
        public Rectangle Area(float x, float y, double now)
        {
            var dirty = new DirtyArea();
            if (!Hidden)
            {
                if (Kind == "laser") dirty.Add(Around(x, y, (int)Math.Ceiling(LaserRadius * 4.4) + 2));
                else
                {
                    var sp = Pointers.Get(Kind, SpriteLength);
                    float k = Squash, lift = Lift;
                    dirty.Add(RectangleF.FromLTRB(x - sp.Tip.X * k, y - sp.Tip.Y * k, x + (sp.Img.Width - sp.Tip.X) * k, y + (sp.Img.Height - sp.Tip.Y) * k));
                    dirty.Add(new RectangleF(x - sp.Tip.X + lift * 0.5f, y - sp.Tip.Y + lift, sp.Img.Width, sp.Img.Height));
                }
                if (EraserRadius > 0) dirty.Add(Around(x, y, (int)Math.Ceiling(EraserRadius) + 4));
            }
            if (TapAge(now) >= 0 && TapAge(now) < TapSeconds) dirty.Add(Around(tapAt.X, tapAt.Y, (int)Math.Ceiling(RefH * 0.05) + 4));
            var r = dirty.Take();
            if (!r.IsEmpty) r.Inflate(3, 3);
            return r;
        }

        public void Paint(Graphics g, float x, float y, double now)
        {
            double age = TapAge(now);
            if (age >= 0 && age < TapSeconds)
            {
                // the tap ring grows out of the tip and fades, so the class sees where the teacher tapped
                float t = (float)(age / TapSeconds), ring = (float)(RefH * (0.012 + 0.036 * t));
                using (var pen = new Pen(Color.FromArgb((int)(220 * (1 - t)), Theme.Yellow), (float)Math.Max(2, RefH * 0.004)))
                    g.DrawEllipse(pen, tapAt.X - ring, tapAt.Y - ring, 2 * ring, 2 * ring);
            }
            if (Hidden) return;
            if (EraserRadius > 0)
            {
                float r = EraserRadius;
                using (var cloth = new SolidBrush(Color.FromArgb(70, 255, 255, 255))) g.FillEllipse(cloth, x - r, y - r, 2 * r, 2 * r);
                using (var dark = new Pen(Color.FromArgb(200, 40, 40, 40), 1.6f)) g.DrawEllipse(dark, x - r, y - r, 2 * r, 2 * r);
                using (var light = new Pen(Color.FromArgb(200, 255, 255, 255), 1f)) g.DrawEllipse(light, x - r + 1.5f, y - r + 1.5f, 2 * r - 3, 2 * r - 3);
            }
            if (Kind == "laser") { Pointers.DrawLaser(g, x, y, LaserRadius, press); return; }
            var sp = Pointers.Get(Kind, SpriteLength);
            float k = Squash, lift = Lift;
            if (Shadow) g.DrawImage(sp.Shadow, x - sp.Tip.X + lift * 0.5f, y - sp.Tip.Y + lift, sp.Shadow.Width, sp.Shadow.Height);
            g.DrawImage(sp.Img, new RectangleF(x - sp.Tip.X * k, y - sp.Tip.Y * k, sp.Img.Width * k, sp.Img.Height * k));
        }
    }

    /// <summary>
    /// The sheets Phấn Trắng writes on: one for the screen itself (ink over a slide) and the numbered board pages.
    /// Board is "none" while writing on the screen, otherwise the kind of board shown.
    /// </summary>
    public sealed class SheetBook
    {
        readonly Sheet screen = new Sheet();
        readonly List<Sheet> pages = new List<Sheet> { new Sheet() };
        int at;

        public string Board = "none";

        public bool OnBoard { get { return Board != "none"; } }
        public Sheet Current { get { return OnBoard ? pages[at] : screen; } }
        public Sheet Screen { get { return screen; } }
        public IList<Sheet> Pages { get { return pages.AsReadOnly(); } }
        public string PageBoard { get { return pages[at].Board; } }
        public string PageLabel { get { return (at + 1) + " / " + pages.Count; } }
        public bool OnFirst { get { return at == 0; } }
        public bool OnLast { get { return at == pages.Count - 1; } }
        /// <summary>Something worth saving as a lesson is on the board pages.</summary>
        public bool HasLesson { get { return pages.Any(p => p.Marks.Any(m => !m.Temporary)); } }

        public void Show(string board)
        {
            Board = string.IsNullOrEmpty(board) ? "none" : board;
            if (OnBoard) pages[at].Board = Board;
        }

        void Follow() { if (OnBoard && !string.IsNullOrEmpty(pages[at].Board)) Board = pages[at].Board; }

        /// <summary>Next page; on the last one a new page is added, but only if the current one has something on it.</summary>
        public bool Next()
        {
            if (OnLast)
            {
                if (pages[at].Marks.Count == 0) return false;
                pages.Add(new Sheet { Board = OnBoard ? Board : null });
            }
            at++;
            Follow();
            return true;
        }

        /// <summary>Previous page; an empty last page is dropped on the way back.</summary>
        public bool Prev()
        {
            if (at == 0) return false;
            if (OnLast && pages[at].Marks.Count == 0) pages.RemoveAt(at);
            at--;
            Follow();
            return true;
        }

        public void Load(IList<Sheet> sheets, string fallbackBoard)
        {
            pages.Clear();
            pages.AddRange(sheets);
            if (pages.Count == 0) pages.Add(new Sheet());
            at = 0;
            Board = string.IsNullOrEmpty(pages[0].Board) ? fallbackBoard : pages[0].Board;
            pages[0].Board = Board;
        }
    }

    /// <summary>
    /// Pictures on a board page: one being placed (it follows the pointer, faint, until the teacher clicks),
    /// and the chosen one, which can be dragged by its body, turned by the round handle or resized by a corner.
    /// </summary>
    public sealed class PictureEditor
    {
        readonly Stage stage;
        PictureMark placing, chosen;
        string grip;                    // "move" | "rotate" | "scale" while dragging
        PointF gripStart, startCenter;
        SizeF startSize;
        float startAngle;

        public PictureEditor(Stage stage) { this.stage = stage; }

        float Knob { get { return (float)Math.Max(9, stage.RefH * 0.011); } }
        float Stem { get { return (float)Math.Max(28, stage.RefH * 0.04); } }
        float FitW { get { return stage.W * 0.55f; } }
        float FitH { get { return stage.H * 0.6f; } }

        public bool Placing { get { return placing != null; } }
        public bool Dragging { get { return grip != null; } }
        /// <summary>What the current drag does: "move", "rotate", "scale", or null.</summary>
        public string Grip { get { return grip; } }
        public PictureMark Chosen { get { return chosen != null && stage.Book.Current.Marks.Contains(chosen) ? chosen : null; } }

        RectangleF Reach(PictureMark p) { float pad = Stem + Knob * 2 + 4; return RectangleF.Inflate(p.Bounds, pad, pad); }

        public void Begin(PictureMark p, PointF at)
        {
            Cancel();
            placing = p;
            p.Alpha = 0.7f;
            p.PlaceAt(at, FitW, FitH);
            stage.Touch(p.Bounds);
        }

        public void Cancel()
        {
            if (placing == null) return;
            stage.Touch(placing.Bounds);
            placing = null;
        }

        public void Follow(PointF at)
        {
            if (placing == null) return;
            var before = placing.Bounds;
            placing.PlaceAt(at, FitW, FitH);
            if (placing.Bounds != before) { stage.Touch(before); stage.Touch(placing.Bounds); }
        }

        public void Drop(PointF at)
        {
            var p = placing;
            if (p == null) return;
            placing = null;
            stage.Touch(p.Bounds);
            p.PlaceAt(at, FitW, FitH);
            p.Alpha = 1;
            p.Start = p.Done = Clock.Now;
            stage.Book.Current.Add(p);
            Choose(p);
        }

        public void Choose(PictureMark p)
        {
            if (chosen == p) return;
            if (chosen != null) stage.Touch(Reach(chosen));
            chosen = p;
            if (p != null) stage.Touch(Reach(p));
        }

        static float Distance(PointF a, PointF b) { float dx = a.X - b.X, dy = a.Y - b.Y; return (float)Math.Sqrt(dx * dx + dy * dy); }

        /// <summary>Starts a drag for a press at p: on a handle of the chosen picture, or on any picture (which becomes chosen).</summary>
        public void Grab(PointF p)
        {
            grip = null;
            var c = Chosen;
            if (c != null)
            {
                var local = c.ToLocal(p);
                float hw = c.Size.Width / 2, hh = c.Size.Height / 2, near = Knob * 1.6f;
                if (Distance(local, new PointF(0, -hh - Stem)) <= near) grip = "rotate";
                else if (new[] { new PointF(-hw, -hh), new PointF(hw, -hh), new PointF(hw, hh), new PointF(-hw, hh) }.Any(k => Distance(local, k) <= near)) grip = "scale";
            }
            if (grip == null)
            {
                var hit = stage.Book.Current.Marks.OfType<PictureMark>().LastOrDefault(m => m.Hit(p, 0));
                Choose(hit);
                if (hit != null) grip = "move";
            }
            if (grip == null || Chosen == null) { grip = null; return; }
            gripStart = p;
            startCenter = chosen.Center; startSize = chosen.Size; startAngle = chosen.Angle;
        }

        public void Drag(PointF p, bool snap)
        {
            var c = Chosen;
            if (c == null || grip == null) return;
            stage.Touch(Reach(c));
            if (grip == "move") c.Center = new PointF(startCenter.X + p.X - gripStart.X, startCenter.Y + p.Y - gripStart.Y);
            else if (grip == "rotate")
            {
                double from = Math.Atan2(gripStart.Y - startCenter.Y, gripStart.X - startCenter.X);
                double to = Math.Atan2(p.Y - startCenter.Y, p.X - startCenter.X);
                double deg = startAngle + (to - from) * 180 / Math.PI;
                c.Angle = (float)(snap ? Math.Round(deg / 15) * 15 : deg);   // Shift: 15° steps
            }
            else
            {
                float k = Distance(p, startCenter) / Math.Max(1, Distance(gripStart, startCenter));
                float smallest = 40f / Math.Min(startSize.Width, startSize.Height);
                float largest = 3f * Math.Max(stage.W, stage.H) / Math.Max(startSize.Width, startSize.Height);
                k = Math.Max(smallest, Math.Min(largest, k));
                c.Size = new SizeF(startSize.Width * k, startSize.Height * k);
            }
            stage.Touch(Reach(c));
        }

        /// <summary>Ends a drag; a real change goes into the page's history as one step.</summary>
        public void Release()
        {
            var c = Chosen;
            bool had = grip != null;
            grip = null;
            if (c == null || !had) return;
            PointF c0 = startCenter, c1 = c.Center;
            SizeF s0 = startSize, s1 = c.Size;
            float a0 = startAngle, a1 = c.Angle;
            if (c0 == c1 && s0 == s1 && a0 == a1) return;
            stage.Book.Current.Record(c, () => { c.Center = c0; c.Size = s0; c.Angle = a0; }, () => { c.Center = c1; c.Size = s1; c.Angle = a1; });
        }

        public void Turn(float degrees)
        {
            var c = Chosen;
            if (c == null) return;
            stage.Touch(Reach(c));
            float before = c.Angle;
            c.Angle = before + degrees;
            float after = c.Angle;
            stage.Book.Current.Record(c, () => c.Angle = before, () => c.Angle = after);
            stage.Touch(Reach(c));
        }

        public void Delete()
        {
            var c = Chosen;
            if (c == null) return;
            stage.Touch(Reach(c));
            stage.Book.Current.Remove(new Mark[] { c });
            chosen = null;
        }

        public void Paint(Graphics g, double now)
        {
            if (placing != null) placing.Draw(g, now);
            var c = Chosen;
            if (c == null) return;
            var saved = g.Save();
            g.TranslateTransform(c.Center.X, c.Center.Y);
            g.RotateTransform(c.Angle);
            float hw = c.Size.Width / 2, hh = c.Size.Height / 2, r = Knob, line = (float)Math.Max(2, stage.RefH * 0.0022);
            var frame = new RectangleF(-hw, -hh, 2 * hw, 2 * hh);
            using (var under = new Pen(Color.FromArgb(160, 0, 0, 0), line + 2))
            using (var dash = new Pen(Theme.Yellow, line) { DashStyle = DashStyle.Dash })
            using (var solid = new Pen(Theme.Yellow, line))
            {
                g.DrawRectangle(under, frame.X, frame.Y, frame.Width, frame.Height);
                g.DrawRectangle(dash, frame.X, frame.Y, frame.Width, frame.Height);
                g.DrawLine(under, 0, -hh, 0, -hh - Stem);
                g.DrawLine(solid, 0, -hh, 0, -hh - Stem);
            }
            using (var white = new SolidBrush(Color.White))
            using (var yellow = new SolidBrush(Theme.Yellow))
            using (var edge = new Pen(Theme.Green, line))
            using (var turn = new Pen(Theme.Green, line) { EndCap = LineCap.ArrowAnchor })
            {
                float s = r * 0.8f;
                foreach (var k in new[] { new PointF(-hw, -hh), new PointF(hw, -hh), new PointF(hw, hh), new PointF(-hw, hh) })
                {
                    g.FillRectangle(white, k.X - s, k.Y - s, 2 * s, 2 * s);
                    g.DrawRectangle(edge, k.X - s, k.Y - s, 2 * s, 2 * s);
                }
                var knob = new RectangleF(-r, -hh - Stem - r, 2 * r, 2 * r);
                g.FillEllipse(yellow, knob);
                g.DrawEllipse(edge, knob);
                g.DrawArc(turn, -r * 0.55f, -hh - Stem - r * 0.55f, r * 1.1f, r * 1.1f, 200, 250);
            }
            g.Restore(saved);
        }
    }

    /// <summary>
    /// Everything shown on one canvas (the overlay over a monitor, or the small try-it area in Settings): the pointer,
    /// the marks on the current sheet, the line being typed and pictures being arranged. The owner moves the pointer
    /// with <see cref="Frame"/>, which returns the area to repaint, and paints it with <see cref="Paint"/>.
    /// </summary>
    public sealed class Stage
    {
        public int W { get; private set; }
        public int H { get; private set; }
        double refH = 1080;
        public double RefH { get { return refH; } set { refH = value; Look.RefH = value; } }

        public readonly PointerLook Look = new PointerLook();
        public readonly SheetBook Book = new SheetBook();
        public readonly PictureEditor Pictures;

        public Color InkColor = Theme.Red;
        public double WidthFactor = 1;
        /// <summary>Seconds after which ink on the screen (not the board) fades; 0 = it stays.</summary>
        public double FadeAfter;
        /// <summary>Seconds after which a "Vẽ tạm" line fades.</summary>
        public double VanishAfter = 2;

        readonly DirtyArea dirty = new DirtyArea();
        bool repaintAll = true;
        string lastLook = "";
        Rectangle lastLookArea = Rectangle.Empty;
        long lastBlink = -1;
        PointF pointer;

        PenMark pen;
        ShapeMark shape;
        List<Mark> wiped;
        TextMark typing;
        RectangleF typingFrame;
        float guideStep;
        /// <summary>Faint ruled lines across the sheet while typing (the teacher can switch them off).</summary>
        public bool TypeLines = true;

        public Stage() { Pictures = new PictureEditor(this); }

        // ---- size & state ----

        public void Resize(int w, int h)
        {
            w = Math.Max(1, w); h = Math.Max(1, h);
            if (w == W && h == H) return;
            W = w; H = h;
            ClearScreen();
            Reset();
        }

        /// <summary>Forget animation state and repaint everything on the next frame.</summary>
        public void Reset() { Look.Reset(); Invalidate(); lastLookArea = Rectangle.Empty; }

        public void Invalidate() { repaintAll = true; lastLook = ""; }

        internal void Touch(RectangleF r) { dirty.Add(r); }

        public Sheet Current { get { return Book.Current; } }
        public string Board { get { return Book.Board; } }
        public bool IsDrawing { get { return pen != null || shape != null || wiped != null; } }
        public bool HasInk { get { return Current.Marks.Count > 0; } }
        public bool CanUndo { get { return Current.CanUndo; } }
        public bool CanRedo { get { return Current.CanRedo; } }
        public float InkWidth { get { return (float)(Math.Max(3, RefH * 0.0055) * WidthFactor); } }
        /// <summary>The ink colour as it will show on the current board (dark ink turns to chalk on a dark board).</summary>
        public Color Ink { get { return Catalog.Readable(InkColor, Board); } }
        public ShapeMark CurrentShape { get { return shape; } }

        /// <summary>Something is still moving by itself (a press relaxing, a tap ring, fading or popping marks).</summary>
        public bool IsSettling
        {
            get
            {
                double now = Clock.Now;
                if (Look.Moving(now)) return true;
                bool fades = FadeAfter > 0 && !Book.OnBoard;
                return Current.Marks.Any(m => (m.Finished && (fades || m.Temporary)) || m.Animating(now));
            }
        }

        // ---- boards & pages ----

        /// <summary>Puts down whatever is in the teacher's hand before switching sheet.</summary>
        void Settle() { EndAll(); Pictures.Choose(null); StopTyping(); }

        public void SetBoard(string kind) { Settle(); Book.Show(kind); repaintAll = true; }
        public void NextPage() { Settle(); Book.Next(); repaintAll = true; }
        public void PrevPage() { Settle(); Book.Prev(); repaintAll = true; }

        public void LoadPages(IList<Sheet> sheets, string fallbackBoard) { Settle(); Book.Load(sheets, fallbackBoard); repaintAll = true; }

        /// <summary>The board pages, with any line in progress finished first (for saving).</summary>
        public IList<Sheet> BoardPages { get { EndAll(); return Book.Pages; } }

        Bitmap paper;
        string paperKey;

        Bitmap PaperBitmap()
        {
            if (!Book.OnBoard) return null;
            string key = Board + "|" + W + "|" + H + "|" + RefH;
            if (paper == null || paperKey != key)
            {
                if (paper != null) paper.Dispose();
                paper = Paper.Make(Board, W, H, RefH);
                paperKey = key;
            }
            return paper;
        }

        // ---- ink ----

        public void BeginStroke(PointF p, bool highlighter, bool temporary = false)
        {
            EndAll();
            var c = Ink;
            if (highlighter && (Catalog.IsDarkBoard(Board) || c.GetBrightness() > 0.85f)) c = Theme.Yellow;   // a pale highlighter would not show
            pen = new PenMark { Color = c, Width = InkWidth, Highlighter = highlighter, Start = Clock.Now, Temporary = temporary };
            pen.Add(p);
            if (temporary) Current.Marks.Add(pen); else Current.Add(pen);
            Touch(pen.Bounds);
        }

        public void ExtendStroke(PointF p)
        {
            if (pen == null) return;
            var last = pen.Pts[pen.Pts.Count - 1];
            if (Math.Abs(p.X - last.X) + Math.Abs(p.Y - last.Y) < 1.5f) return;   // ignore jitter
            Touch(pen.Bounds);
            pen.Add(p);
            Touch(pen.Bounds);
        }

        public void BeginShape(string kind, PointF from, PointF to)
        {
            EndAll();
            shape = new ShapeMark { Kind = kind, P0 = from, Color = Ink, Width = InkWidth, Start = Clock.Now };
            shape.SetEnd(to);
            Current.Add(shape);
            Touch(shape.Bounds);
        }

        public void UpdateShape(PointF to)
        {
            if (shape == null) return;
            Touch(shape.Bounds);
            shape.SetEnd(to);
            Touch(shape.Bounds);
        }

        public void AddStamp(string kind, PointF center)
        {
            double now = Clock.Now;
            var s = new StampMark { Kind = kind, Center = center, Size = (float)(RefH * 0.085), Start = now, Done = now };
            s.Layout();
            Current.Add(s);
            Touch(s.Bounds);
        }

        /// <summary>The eraser lifts every mark it passes over; the whole wipe becomes one undo step when it ends.</summary>
        public void EraseAt(PointF p)
        {
            if (wiped == null) wiped = new List<Mark>();
            float r = Look.EraserRadius > 0 ? Look.EraserRadius : InkWidth * 3;
            var reach = new RectangleF(p.X - r, p.Y - r, 2 * r, 2 * r);
            foreach (var m in Current.Marks.Where(m => m.Bounds.IntersectsWith(reach) && m.Hit(p, r)).ToList())
            {
                wiped.Add(m);
                Current.Lift(m);
                Touch(m.Bounds);
            }
        }

        /// <summary>Finishes the line, shape, wipe or picture drag in progress.</summary>
        public void EndAll()
        {
            double now = Clock.Now;
            Pictures.Release();
            if (pen != null) { pen.Done = now; pen = null; }
            if (shape != null) { shape.Done = now; shape = null; }
            if (wiped != null)
            {
                var gone = wiped;
                wiped = null;
                Current.Marks.AddRange(gone);   // put back for a moment so the removal is recorded as one step
                Current.Remove(gone);
            }
        }

        // undo may move a picture away from its old place: repaint everything (rare, so cheap enough)
        public void Undo() { EndAll(); Current.Undo(); repaintAll = true; }
        public void Redo() { EndAll(); Current.Redo(); repaintAll = true; }

        public void ClearInk()
        {
            EndAll();
            var all = Current.Marks.ToList();
            foreach (var m in all) Touch(m.Bounds);
            Current.Remove(all);
            Current.Caret = null;
        }

        /// <summary>Wipes the screen sheet completely (history too): used when Phấn Trắng is switched off.</summary>
        public void ClearScreen()
        {
            pen = null; shape = null; wiped = null;
            foreach (var m in Book.Screen.Marks) Touch(m.Bounds);
            Book.Screen.Clear();
            StopTyping();
        }

        // ---- typing ----

        /// <summary>Where the next typed line starts on the current sheet.</summary>
        public PointF Caret
        {
            get { return Current.Caret ?? new PointF(W * 0.07f, H * 0.08f); }
            set { Current.Caret = value; }
        }

        public bool Typing { get { return typing != null; } }

        /// <summary>Shows the line being typed, with a blinking caret, at the sheet's caret.</summary>
        public void ShowTyping(string text, string fontId, float px)
        {
            if (typing == null) typing = new TextMark { ShowCaret = true, Start = Clock.Now };
            else Touch(typing.Bounds);
            typing.Text = text ?? "";
            typing.FontId = fontId;
            typing.Px = px;
            typing.Color = Ink;
            typing.Pos = Caret;
            typing.Layout();
            Touch(typing.Bounds);
            Touch(typingFrame);
            typingFrame = LineFrame(typing);
            Touch(typingFrame);
            float step = TypeLines ? typing.LineHeight : 0;
            if (step != guideStep) { guideStep = step; repaintAll = true; }
        }

        public void StopTyping()
        {
            if (typing == null) return;
            Touch(typing.Bounds);
            Touch(typingFrame);
            typingFrame = RectangleF.Empty;
            if (guideStep > 0) { guideStep = 0; repaintAll = true; }
            typing = null;
        }

        /// <summary>Where the ruled lines start: the first line sits where typing starts on an empty sheet.</summary>
        float GuideTop { get { return H * 0.08f; } }

        /// <summary>The top of the ruled line a click at height y falls on (lines are one text line apart).</summary>
        public float SnapToLine(float y, float px)
        {
            float step = Math.Max(1, px * 1.3f), top = GuideTop;
            float row = (float)Math.Floor((y - top) / step);
            float at = top + row * step;
            while (at < 0) at += step;
            return at;
        }

        /// <summary>A dashed box around the line being typed, like a text box in PowerPoint: it shows where the words go.</summary>
        RectangleF LineFrame(TextMark t)
        {
            float pad = t.Px * 0.25f;
            float right = Math.Max(t.Bounds.Right - 12, t.Pos.X + W * 0.3f);
            right = Math.Min(right, W - 6);
            return RectangleF.FromLTRB(Math.Max(2, t.Pos.X - pad), Math.Max(2, t.Pos.Y - t.Px * 0.12f), Math.Max(t.Pos.X + pad, right), Math.Min(H - 2, t.Pos.Y + t.LineHeight + t.Px * 0.05f));
        }

        void PaintGuides(Graphics g, Rectangle area)
        {
            if (typing == null) return;
            float width = Math.Max(1f, H / 900f);
            if (guideStep > 0)
                using (var line = new Pen(Color.FromArgb(60, typing.Color), width) { DashPattern = new[] { 6f, 5f } })
                    for (float y = GuideTop + typing.Px * 1.18f - guideStep * (float)Math.Floor(GuideTop / guideStep); y < H; y += guideStep)
                        if (y >= area.Top - 2 && y <= area.Bottom + 2) g.DrawLine(line, 0, y, W, y);
            if (typingFrame.Width > 0)
                using (var box = new Pen(Color.FromArgb(170, Theme.Yellow), Math.Max(1.5f, width * 1.5f)) { DashPattern = new[] { 4f, 3f } })
                    g.DrawRectangle(box, typingFrame.X, typingFrame.Y, typingFrame.Width, typingFrame.Height);
        }

        /// <summary>Puts the typed line on the sheet (undoable) and moves the caret one line down.</summary>
        public void CommitTyping()
        {
            if (typing == null) return;
            var t = typing;
            if (t.Text.Trim().Length > 0)
            {
                double now = Clock.Now;
                var line = new TextMark { Text = t.Text.TrimEnd(), FontId = t.FontId, Px = t.Px, Pos = t.Pos, Color = t.Color, Start = now, Done = now };
                line.Layout();
                Current.Add(line);
                Touch(line.Bounds);
            }
            Caret = new PointF(t.Pos.X, t.Pos.Y + t.LineHeight);
            ShowTyping("", t.FontId, t.Px);
        }

        /// <summary>Takes back the last typed line on this sheet ("Xoá dòng cuối") and puts the caret where it was.</summary>
        public void RemoveLastLine()
        {
            var last = Current.Marks.OfType<TextMark>().LastOrDefault();
            if (last == null) return;
            Current.Remove(new Mark[] { last });
            Touch(last.Bounds);
            Caret = last.Pos;
            if (typing != null) ShowTyping(typing.Text, typing.FontId, typing.Px);
        }

        // ---- frames ----

        /// <summary>Fades screen ink and "Vẽ tạm" lines, removing them once gone; keeps popping stamps and the caret moving.</summary>
        void Age(double now)
        {
            const double FadeLength = 0.6;
            var marks = Current.Marks;
            bool screenFades = FadeAfter > 0 && !Book.OnBoard;
            for (int i = marks.Count - 1; i >= 0; i--)
            {
                var m = marks[i];
                if (m.Animating(now)) Touch(m.Bounds);
                if (!m.Finished || !(m.Temporary || screenFades))
                {
                    if (m.Alpha != 1) { m.Alpha = 1; Touch(m.Bounds); }
                    continue;
                }
                double t = now - m.Done - (m.Temporary ? VanishAfter : Math.Max(0.5, FadeAfter));
                if (t <= 0) continue;
                Touch(m.Bounds);
                if (t >= FadeLength) { marks.RemoveAt(i); continue; }
                double k = t / FadeLength;
                m.Alpha = (float)(1 - k * k);
            }
            long blink = (long)(now * 2);
            if (typing != null && blink != lastBlink) Touch(typing.Bounds);
            lastBlink = blink;
        }

        /// <summary>Moves the pointer to (x, y) and advances animations by dt seconds. Returns the area to repaint.</summary>
        public Rectangle Frame(double x, double y, double dt, double now)
        {
            if (W <= 1 || H <= 1) return Rectangle.Empty;
            pointer = new PointF((float)x, (float)y);
            Look.Advance(dt);
            Age(now);
            Pictures.Follow(pointer);

            string state = Look.State(x, y, now);
            if (state != lastLook || repaintAll)
            {
                var area = Look.Area(pointer.X, pointer.Y, now);
                dirty.Add(lastLookArea);
                dirty.Add(area);
                lastLookArea = area;
                lastLook = state;
            }
            var r = dirty.Take();
            if (repaintAll) { r = new Rectangle(0, 0, W, H); repaintAll = false; }
            if (r.IsEmpty) return r;
            r.Inflate(2, 2);
            return Rectangle.Intersect(r, new Rectangle(0, 0, W, H));
        }

        /// <summary>Repaints 'area': the board (or the given backdrop, or transparency), then marks, typing, pictures and the pointer.</summary>
        public void Paint(Graphics g, Rectangle area, Bitmap backdrop = null)
        {
            var ground = PaperBitmap() ?? backdrop;
            g.SetClip(area);
            g.CompositingMode = CompositingMode.SourceCopy;
            if (ground != null)
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.None;
                g.DrawImage(ground, area, area, GraphicsUnit.Pixel);
            }
            else g.Clear(Color.Transparent);
            g.CompositingMode = CompositingMode.SourceOver;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.InterpolationMode = InterpolationMode.Bilinear;

            double now = Clock.Now;
            PaintGuides(g, area);
            foreach (var m in Current.Marks) if (m.Bounds.IntersectsWith(area)) m.Draw(g, now);
            if (typing != null) typing.Draw(g, now);
            Pictures.Paint(g, now);
            Look.Paint(g, pointer.X, pointer.Y, now);
            g.ResetClip();
        }
    }

    /// <summary>The see-through window covering the monitor Phấn Trắng is working on, showing a <see cref="Stage"/>.</summary>
    public sealed class ScreenLayer
    {
        readonly GlassWindow glass = new GlassWindow(true);
        public readonly Stage Stage = new Stage();
        PixelBuffer buffer;
        RECT monitor;
        bool sendAll = true;

        public bool Visible { get; private set; }
        public RECT Monitor { get { return monitor; } }

        public void Show()
        {
            if (Visible) return;
            Visible = true;
            Stage.Reset();
            sendAll = true;
            glass.Show();
        }

        public void Hide()
        {
            if (!Visible) return;
            Visible = false;
            glass.Hide();
            Stage.Reset();
        }

        public void Raise() { glass.Raise(); }

        /// <summary>Moves the layer to another monitor (or adapts to a resolution change).</summary>
        public void Cover(RECT m)
        {
            if (buffer != null && m.SameAs(monitor)) return;
            monitor = m;
            if (buffer == null || buffer.W != m.Width || buffer.H != m.Height)
            {
                if (buffer != null) buffer.Dispose();
                buffer = new PixelBuffer(m.Width, m.Height);
            }
            Stage.RefH = m.Height;
            Stage.Resize(m.Width, m.Height);
            Stage.Reset();
            sendAll = true;
        }

        public void Frame(double x, double y, double dt, double now)
        {
            if (buffer == null || !Visible) return;
            var area = Stage.Frame(x, y, dt, now);
            if (sendAll) area = new Rectangle(0, 0, buffer.W, buffer.H);
            if (area.IsEmpty) return;
            Stage.Paint(buffer.G, area);
            buffer.Show(glass.Handle, monitor.Left, monitor.Top, sendAll ? (Rectangle?)null : area);
            sendAll = false;
        }
    }
}
