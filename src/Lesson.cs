using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace PhanTrang
{
    /// <summary>
    /// A prepared lesson: the board pages saved to a .phantrang file (JSON). Positions are stored as a fraction
    /// of the screen height, so a lesson made on a laptop opens at the right size on the classroom projector.
    /// Lesson files travel between teachers (USB, Zalo), so reading treats them as untrusted: sizes and counts are
    /// capped, every number is checked, and anything unexpected becomes a readable InvalidDataException.
    /// </summary>
    public static class Lesson
    {
        public const string Ext = ".phantrang";
        public const string Filter = "Bài giảng Phấn Trắng (*.phantrang)|*.phantrang";
        public static string AutoSavePath { get { return Path.Combine(Settings.Dir, "bai-dang-soan" + Ext); } }
        const string OlderPrefix = "bai-dang-soan-", OlderStamp = "yyyyMMdd-HHmmss";
        const int OlderKept = 5;

        /// <summary>
        /// Moves the spare file of an earlier session aside (bai-dang-soan-yyyyMMdd-HHmmss.phantrang) before this
        /// session writes its own, so unsaved work from yesterday is never overwritten by today's. Keeps the newest few.
        /// </summary>
        public static void KeepEarlierAutoSave()
        {
            try
            {
                var spare = new FileInfo(AutoSavePath);
                if (!spare.Exists) return;
                string aside = Path.Combine(Settings.Dir, OlderPrefix + spare.LastWriteTime.ToString(OlderStamp, Inv) + Ext);
                if (File.Exists(aside)) File.Delete(spare.FullName);   // already kept (same moment): nothing new in it
                else File.Move(spare.FullName, aside);
                foreach (var old in OlderAutoSaves().Skip(OlderKept)) File.Delete(old.Key);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        /// <summary>Earlier sessions' spare files, newest first: path and when it was written.</summary>
        public static List<KeyValuePair<string, DateTime>> OlderAutoSaves()
        {
            var list = new List<KeyValuePair<string, DateTime>>();
            try
            {
                if (!Directory.Exists(Settings.Dir)) return list;
                foreach (var f in Directory.GetFiles(Settings.Dir, OlderPrefix + "*" + Ext))
                {
                    DateTime when;
                    string stamp = Path.GetFileNameWithoutExtension(f).Substring(OlderPrefix.Length);
                    if (DateTime.TryParseExact(stamp, OlderStamp, Inv, DateTimeStyles.None, out when)) list.Add(new KeyValuePair<string, DateTime>(f, when));
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return list.OrderByDescending(p => p.Value).ToList();
        }

        // limits for files from elsewhere
        public const long MaxFileBytes = 200L << 20;
        public const int MaxPages = 300, MaxItemsPerPage = 20000, MaxPointsPerStroke = 50000, MaxTextLength = 2000;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static double R(double v) { return Math.Round(v, 5); }
        static string Hex(Color c) { return c.ToArgb().ToString("X8"); }

        static Color Col(object o)
        {
            int v;
            var s = o as string;
            if (s != null && s.Length == 8 && int.TryParse(s, NumberStyles.HexNumber, Inv, out v)) return Color.FromArgb(v);
            return Theme.Red;
        }

        /// <summary>A finite number in [lo, hi]; anything else (text, NaN, Infinity, out of range) is rejected.</summary>
        static double Num(object o, double lo, double hi)
        {
            double d;
            try { d = Convert.ToDouble(o, Inv); } catch { throw new InvalidDataException("Số không hợp lệ."); }
            if (double.IsNaN(d) || double.IsInfinity(d) || d < lo || d > hi) throw new InvalidDataException("Số nằm ngoài khoảng cho phép.");
            return d;
        }

        // ---------------- save ----------------

        public static void Save(string path, IList<Sheet> pages, int w, int h, string currentBoard)
        {
            double H = Math.Max(1, h);
            var list = new List<object>();
            foreach (var p in pages)
            {
                var items = new List<object>();
                foreach (var it in p.Marks)
                {
                    if (it.Temporary) continue;
                    var d = ToDict(it, H);
                    if (d != null) items.Add(d);
                }
                list.Add(new Dictionary<string, object> { { "board", p.Board ?? currentBoard }, { "items", items } });
            }
            var doc = new Dictionary<string, object>
            {
                { "app", "PhanTrang" }, { "version", 1 }, { "aspect", R(w / H) },
                { "saved", DateTime.Now.ToString("s", Inv) }, { "pages", list },
            };
            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            string full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            // write next to it, then swap: a crash or a full disk never leaves a half-written lesson behind
            string tmp = full + ".tmp";
            File.WriteAllText(tmp, js.Serialize(doc), new UTF8Encoding(false));
            if (File.Exists(full)) File.Replace(tmp, full, null);
            else File.Move(tmp, full);
        }

        static Dictionary<string, object> ToDict(Mark it, double H)
        {
            var s = it as PenMark;
            if (s != null)
            {
                var pts = new List<double>();
                foreach (var p in s.Pts) { pts.Add(R(p.X / H)); pts.Add(R(p.Y / H)); }
                return new Dictionary<string, object> { { "t", "stroke" }, { "c", Hex(s.Color) }, { "w", R(s.Width / H) }, { "hl", s.Highlighter }, { "pts", pts } };
            }
            var sh = it as ShapeMark;
            if (sh != null)
                return new Dictionary<string, object> { { "t", "shape" }, { "k", sh.Kind }, { "c", Hex(sh.Color) }, { "w", R(sh.Width / H) },
                    { "p", new[] { R(sh.P0.X / H), R(sh.P0.Y / H), R(sh.P1.X / H), R(sh.P1.Y / H) } } };
            var t = it as TextMark;
            if (t != null)
                return new Dictionary<string, object> { { "t", "text" }, { "s", t.Text }, { "f", t.FontId }, { "c", Hex(t.Color) }, { "px", R(t.Px / H) },
                    { "x", R(t.Pos.X / H) }, { "y", R(t.Pos.Y / H) } };
            var st = it as StampMark;
            if (st != null)
                return new Dictionary<string, object> { { "t", "stamp" }, { "k", st.Kind }, { "x", R(st.Center.X / H) }, { "y", R(st.Center.Y / H) }, { "size", R(st.Size / H) } };
            var im = it as PictureMark;
            if (im != null && im.Data != null)
                return new Dictionary<string, object> { { "t", "image" }, { "data", Convert.ToBase64String(im.Data) },
                    { "x", R(im.Rect.X / H) }, { "y", R(im.Rect.Y / H) }, { "w", R(im.Rect.Width / H) }, { "h", R(im.Rect.Height / H) }, { "r", Math.Round(im.Angle, 2) } };
            return null;
        }

        // ---------------- open ----------------

        /// <summary>Reads a lesson for a w x h screen. Any problem with the file becomes an InvalidDataException with a message for the teacher.</summary>
        public static List<Sheet> Load(string path, int w, int h)
        {
            var info = new FileInfo(path);
            if (!info.Exists) throw new FileNotFoundException("Không tìm thấy tệp bài giảng.", path);
            if (info.Length > MaxFileBytes) throw new InvalidDataException("Tệp quá lớn để là một bài giảng.");
            return Parse(File.ReadAllText(path, Encoding.UTF8), w, h);
        }

        public static List<Sheet> Parse(string json, int w, int h)
        {
            Dictionary<string, object> doc;
            try
            {
                var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 16 };
                doc = js.DeserializeObject(json) as Dictionary<string, object>;
            }
            catch (Exception e) { throw new InvalidDataException("Tệp bị hỏng hoặc không phải bài giảng Phấn Trắng.", e); }
            object pagesObj;
            if (doc == null || !doc.TryGetValue("pages", out pagesObj) || !(pagesObj is object[])) throw new InvalidDataException("Tệp này không phải bài giảng Phấn Trắng.");
            var pageList = (object[])pagesObj;
            if (pageList.Length == 0) throw new InvalidDataException("Bài giảng này chưa có trang nào.");
            if (pageList.Length > MaxPages) throw new InvalidDataException("Bài giảng có quá nhiều trang (tối đa " + MaxPages + ").");

            double H = Math.Max(1, h);
            double aspect = w / H;
            object a;
            if (doc.TryGetValue("aspect", out a)) { try { aspect = Num(a, 0.2, 6); } catch (InvalidDataException) { } }
            float offX = (float)((w - aspect * H) / 2);
            double now = Clock.Now - 5;   // finished long ago: no pop-in animation
            var pages = new List<Sheet>();
            foreach (var po in pageList)
            {
                var pd = po as Dictionary<string, object>;
                if (pd == null) continue;
                var page = new Sheet();
                object b;
                if (pd.TryGetValue("board", out b) && b is string && Catalog.Boards.Any(x => x[0] == (string)b && x[0] != "none")) page.Board = (string)b;
                object items;
                if (pd.TryGetValue("items", out items) && items is object[])
                {
                    var arr = (object[])items;
                    if (arr.Length > MaxItemsPerPage) throw new InvalidDataException("Một trang có quá nhiều nét vẽ.");
                    foreach (var io in arr)
                    {
                        var it = FromDict(io as Dictionary<string, object>, H, offX);
                        if (it == null) continue;   // an item we cannot read is skipped, the rest of the lesson still opens
                        it.Start = now; it.Done = now;
                        page.Marks.Add(it);
                    }
                }
                pages.Add(page);
            }
            if (pages.Count == 0) throw new InvalidDataException("Bài giảng này chưa có trang nào.");
            return pages;
        }

        // positions may sit a little off the page; sizes must be sensible (all in screen heights)
        const double PosLo = -2, PosHi = 8;

        static PointF P(object x, object y, double H, float offX)
        {
            return new PointF((float)(Num(x, PosLo, PosHi) * H) + offX, (float)(Num(y, PosLo, PosHi) * H));
        }

        static string Str(Dictionary<string, object> d, string key) { object o; return d.TryGetValue(key, out o) ? o as string : null; }

        static Mark FromDict(Dictionary<string, object> d, double H, float offX)
        {
            if (d == null) return null;
            try
            {
                switch (Str(d, "t"))
                {
                    case "stroke":
                        var a = d["pts"] as object[];
                        if (a == null || a.Length < 2 || a.Length > MaxPointsPerStroke * 2) return null;
                        object hl;
                        var s = new PenMark { Color = Col(d["c"]), Width = (float)(Num(d["w"], 0.0005, 0.1) * H), Highlighter = d.TryGetValue("hl", out hl) && true.Equals(hl) };
                        var pts = new List<PointF>(a.Length / 2);
                        for (int i = 0; i + 1 < a.Length; i += 2) pts.Add(P(a[i], a[i + 1], H, offX));
                        s.AddRange(pts);
                        return s;
                    case "shape":
                        var p = d["p"] as object[];
                        string kind = Str(d, "k");
                        if (p == null || p.Length != 4 || !Catalog.Shapes.Any(x => x[0] == kind)) return null;
                        var sh = new ShapeMark { Kind = kind, Color = Col(d["c"]), Width = (float)(Num(d["w"], 0.0005, 0.1) * H), P0 = P(p[0], p[1], H, offX) };
                        sh.SetEnd(P(p[2], p[3], H, offX));
                        return sh;
                    case "text":
                        string font = Str(d, "f") ?? "mali";
                        if (!Catalog.TextFonts.Any(f => f[0] == font)) font = "mali";
                        string text = Str(d, "s") ?? "";
                        if (text.Length > MaxTextLength) text = text.Substring(0, MaxTextLength);
                        var t = new TextMark { Text = text, FontId = font, Color = Col(d["c"]), Px = (float)(Num(d["px"], 0.005, 0.5) * H), Pos = P(d["x"], d["y"], H, offX) };
                        t.Layout();
                        return t;
                    case "stamp":
                        string sk = Str(d, "k");
                        if (!Catalog.Stamps.Any(x => x[0] == sk)) return null;
                        var st = new StampMark { Kind = sk, Center = P(d["x"], d["y"], H, offX), Size = (float)(Num(d["size"], 0.01, 0.5) * H) };
                        st.Layout();
                        return st;
                    case "image":
                        string data = Str(d, "data");
                        if (data == null || data.Length > PictureMark.MaxFileBytes * 4 / 3 + 8) return null;
                        var im = PictureMark.FromBytes(Convert.FromBase64String(data), true);
                        var at = P(d["x"], d["y"], H, offX);
                        im.Rect = new RectangleF(at.X, at.Y, (float)(Num(d["w"], 0.005, 6) * H), (float)(Num(d["h"], 0.005, 6) * H));
                        object rot;
                        if (d.TryGetValue("r", out rot)) im.Angle = (float)Num(rot, -3600, 3600);
                        return im;
                }
            }
            catch (Exception) { }
            return null;
        }

        // ---------------- pictures ----------------

        /// <summary>The pages worth exporting: all pages with something on them (or the only page there is).</summary>
        static List<int> Exportable(IList<Sheet> pages)
        {
            var list = new List<int>();
            for (int i = 0; i < pages.Count; i++)
                if (pages[i].Marks.Count > 0 || pages.Count == 1) list.Add(i);
            return list;
        }

        /// <summary>
        /// One board page as it looks on a w x h screen, drawn 'scale' times larger, every mark fully visible
        /// ("Vẽ tạm" lines left out).
        /// </summary>
        static Bitmap RenderPage(Sheet page, int w, int h, string currentBoard, double scale = 1)
        {
            string board = page.Board ?? (currentBoard == "none" ? "white" : currentBoard);
            int pw = (int)Math.Round(w * scale), ph = (int)Math.Round(h * scale);
            var bmp = Paper.Make(board, pw, ph, ph);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.ScaleTransform((float)scale, (float)scale);
                foreach (var m in page.Marks)
                {
                    if (m.Temporary) continue;
                    float a = m.Alpha; m.Alpha = 1; m.Draw(g, 1e9); m.Alpha = a;
                }
            }
            return bmp;
        }

        /// <summary>Saves every page as "name-1.png", "name-2.png"… next to path. Returns how many were written.</summary>
        public static int ExportImages(string path, IList<Sheet> pages, int w, int h, string currentBoard)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            string stem = Path.GetFileNameWithoutExtension(path);
            var chosen = Exportable(pages);
            foreach (int i in chosen)
                using (var bmp = RenderPage(pages[i], w, h, currentBoard))
                    bmp.Save(Path.Combine(dir, stem + "-" + (i + 1) + ".png"), ImageFormat.Png);
            return chosen.Count;
        }

        /// <summary>Pages are drawn at least this tall in a PDF, so text stays sharp when printed or zoomed.</summary>
        const int PdfMinHeight = 1080;

        /// <summary>
        /// The whole lesson as one PDF, one board page per PDF page, landscape in the screen's proportions
        /// (to print, hand out or send for revision). Returns how many pages were written.
        /// </summary>
        public static int ExportPdf(string path, IList<Sheet> pages, int w, int h, string currentBoard, string title)
        {
            // small screens are drawn larger (positions are all relative to the height, so the page simply scales up)
            double k = Math.Max(1.0, PdfMinHeight / (double)Math.Max(1, h));
            int pw = (int)Math.Round(w * k), ph = (int)Math.Round(h * k);
            var jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            var list = new List<PdfPage>();
            foreach (int i in Exportable(pages))
            {
                using (var bmp = RenderPage(pages[i], w, h, currentBoard, k))
                using (var ms = new MemoryStream())
                using (var q = new EncoderParameters(1))
                {
                    q.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);
                    bmp.Save(ms, jpeg, q);
                    list.Add(new PdfPage { Jpeg = ms.ToArray(), PixelWidth = pw, PixelHeight = ph });
                }
            }
            PdfWriter.Write(path, list, 842, title);   // 842 pt = the long side of an A4 sheet
            return list.Count;
        }
    }
}
