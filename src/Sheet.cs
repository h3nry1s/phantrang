using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

namespace PhanTrang
{
    /// <summary>
    /// One sheet of marks: the screen itself, or one page of the board. Every change made through Add, Remove or
    /// Record goes into the sheet's own history, a list of reversible edits with a cursor (edits after the cursor
    /// are the ones that can be redone). The history keeps the last <see cref="HistoryLimit"/> edits.
    /// </summary>
    public class Sheet
    {
        public const int HistoryLimit = 500;

        public readonly List<Mark> Marks = new List<Mark>();
        /// <summary>Where the next typed line goes on this sheet.</summary>
        public PointF? Caret;
        /// <summary>The board kind this page shows (each page of a prepared lesson keeps its own).</summary>
        public string Board;

        /// <summary>An edit that can be taken back and done again. Each returns false if it no longer applies.</summary>
        sealed class Edit
        {
            public Func<bool> Back, Again;
        }

        readonly List<Edit> history = new List<Edit>();
        int cursor;   // history[0 .. cursor) are done; history[cursor ..) were undone

        public bool CanUndo { get { return cursor > 0; } }
        public bool CanRedo { get { return cursor < history.Count; } }

        void Push(Edit e)
        {
            if (cursor < history.Count) history.RemoveRange(cursor, history.Count - cursor);
            history.Add(e);
            if (history.Count > HistoryLimit) history.RemoveRange(0, history.Count - HistoryLimit);
            cursor = history.Count;
        }

        bool Put(IEnumerable<Mark> marks)
        {
            bool any = false;
            foreach (var m in marks) if (!Marks.Contains(m)) { Marks.Add(m); any = true; }
            return any;
        }

        bool Take(IEnumerable<Mark> marks)
        {
            bool any = false;
            foreach (var m in marks) any |= Marks.Remove(m);
            return any;
        }

        public void Add(Mark m)
        {
            Marks.Add(m);
            var one = new[] { m };
            Push(new Edit { Back = () => Take(one), Again = () => Put(one) });
        }

        /// <summary>Removes the given marks (those still on the sheet) as one undoable step.</summary>
        public void Remove(IEnumerable<Mark> marks)
        {
            var gone = marks.Where(Marks.Contains).ToArray();
            if (gone.Length == 0) return;
            Take(gone);
            Push(new Edit { Back = () => Put(gone), Again = () => Take(gone) });
        }

        /// <summary>A change already made to a mark in place (a picture moved or turned), so it can be undone.</summary>
        public void Record(Mark m, Action undo, Action redo)
        {
            Push(new Edit
            {
                Back = () => { if (!Marks.Contains(m)) return false; undo(); return true; },
                Again = () => { if (!Marks.Contains(m)) return false; redo(); return true; },
            });
        }

        /// <summary>Takes a mark off without a history entry (the eraser lifts marks while it is still moving).</summary>
        public void Lift(Mark m) { Marks.Remove(m); }

        /// <summary>Undoes the latest edit that still changes something. False if there was nothing to undo.</summary>
        public bool Undo()
        {
            while (cursor > 0)
                if (history[--cursor].Back()) return true;
            return false;
        }

        public bool Redo()
        {
            while (cursor < history.Count)
                if (history[cursor++].Again()) return true;
            return false;
        }

        public void Clear()
        {
            Marks.Clear();
            history.Clear();
            cursor = 0;
            Caret = null;
        }
    }

    /// <summary>Board backgrounds: chalkboards and Vietnamese school paper, drawn to any size.</summary>
    public static class Paper
    {
        public static Bitmap Make(string kind, int w, int h, double refH)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp)) Paint(g, kind, new Rectangle(0, 0, w, h), refH);
            return bmp;
        }

        /// <summary>Paints the board into r. refH is the height the rulings are sized for (the real screen's height).</summary>
        public static void Paint(Graphics g, string kind, Rectangle r, double refH)
        {
            var saved = g.Save();
            g.SmoothingMode = SmoothingMode.None;
            g.TranslateTransform(r.X, r.Y);
            int w = r.Width, h = r.Height;
            switch (kind)
            {
                case "green": Chalkboard(g, w, h, refH); break;
                case "black": Fill(g, Color.FromArgb(28, 30, 33), w, h); break;
                case "oli": ExerciseBook(g, w, h, refH); break;
                case "grid": SquarePaper(g, w, h, refH); break;
                case "lines": RuledPaper(g, w, h, refH); break;
                default: Fill(g, Color.FromArgb(253, 253, 251), w, h); break;
            }
            g.Restore(saved);
        }

        static void Fill(Graphics g, Color c, int w, int h)
        {
            using (var b = new SolidBrush(c)) g.FillRectangle(b, 0, 0, w, h);
        }

        static void Chalkboard(Graphics g, int w, int h, double refH)
        {
            var area = new Rectangle(0, 0, Math.Max(1, w), Math.Max(1, h));
            using (var shade = new LinearGradientBrush(area, Color.FromArgb(40, 90, 68), Color.FromArgb(30, 70, 53), 60f)) g.FillRectangle(shade, area);
            float edge = (float)Math.Max(4, refH * 0.016);
            using (var wood = new Pen(Theme.Frame, edge)) g.DrawRectangle(wood, edge / 2, edge / 2, w - edge, h - edge);
        }

        /// <summary>Lines at 'start', 'start + step', … across the whole height; every 'boldEvery'-th one stronger.</summary>
        static void Rows(Graphics g, Pen thin, Pen bold, int boldEvery, float start, float step, int w, int h)
        {
            int n = 0;
            for (float y = start; y <= h; y += step, n++) g.DrawLine(n % boldEvery == 0 ? bold : thin, 0, y, w, y);
        }

        /// <summary>Vở ô li: tall squares, each split into 4 rows by 3 thin lines, with a red margin.</summary>
        static void ExerciseBook(Graphics g, int w, int h, double refH)
        {
            Fill(g, Color.FromArgb(253, 252, 247), w, h);
            float square = (float)(refH / 18), left = square * 2.5f;
            using (var thin = new Pen(Color.FromArgb(214, 204, 236), 1))
            using (var bold = new Pen(Color.FromArgb(160, 140, 214), 1.4f))
            using (var margin = new Pen(Color.FromArgb(229, 90, 90), 1.6f))
            {
                Rows(g, thin, bold, 4, square * 0.6f, square / 4, w, h);
                for (float x = left; x < w; x += square) g.DrawLine(bold, x, 0, x, h);
                g.DrawLine(margin, left - square * 0.12f, 0, left - square * 0.12f, h);
            }
        }

        static void SquarePaper(Graphics g, int w, int h, double refH)
        {
            Fill(g, Color.FromArgb(252, 253, 255), w, h);
            float cell = (float)(refH / 24);
            using (var thin = new Pen(Color.FromArgb(205, 222, 242), 1))
            using (var bold = new Pen(Color.FromArgb(160, 192, 230), 1.2f))
            {
                Rows(g, thin, bold, 5, 0, cell, w, h);
                int n = 0;
                for (float x = 0; x < w; x += cell, n++) g.DrawLine(n % 5 == 0 ? bold : thin, x, 0, x, h);
            }
        }

        static void RuledPaper(Graphics g, int w, int h, double refH)
        {
            Fill(g, Color.FromArgb(253, 253, 250), w, h);
            float gap = (float)(refH / 15);
            using (var rule = new Pen(Color.FromArgb(170, 200, 235), 1.3f))
            using (var margin = new Pen(Color.FromArgb(229, 110, 110), 1.6f))
            {
                Rows(g, rule, rule, 1, gap * 1.6f, gap, w, h);
                g.DrawLine(margin, gap * 1.8f, 0, gap * 1.8f, h);
            }
        }
    }
}
