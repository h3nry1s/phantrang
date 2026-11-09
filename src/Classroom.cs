using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace PhanTrang
{
    /// <summary>
    /// The class list: the pupils' names, read from a text file, a CSV or an Excel sheet, or typed one per line.
    /// Kept only on this computer (%APPDATA%\PhanTrang\danh-sach-lop.txt), never sent anywhere.
    /// </summary>
    public static class ClassRoster
    {
        public const int MaxNames = 300, MaxNameLength = 60;
        const long MaxFileBytes = 20L << 20;

        public static string SavedPath { get { return Path.Combine(Settings.Dir, "danh-sach-lop.txt"); } }

        /// <summary>The saved list (empty if there is none yet).</summary>
        public static List<string> Load()
        {
            try { return File.Exists(SavedPath) ? Clean(File.ReadAllLines(SavedPath, Encoding.UTF8)) : new List<string>(); }
            catch (IOException) { return new List<string>(); }
            catch (UnauthorizedAccessException) { return new List<string>(); }
        }

        public static void Save(IList<string> names)
        {
            Directory.CreateDirectory(Settings.Dir);
            File.WriteAllLines(SavedPath, Clean(names), new UTF8Encoding(false));
        }

        /// <summary>One name per line: blank lines, numbering ("1.", "02)", "3 -") and repeated spaces are removed; duplicates stay (two pupils may share a name).</summary>
        public static List<string> Clean(IEnumerable<string> lines)
        {
            var list = new List<string>();
            if (lines == null) return list;
            foreach (var raw in lines)
            {
                if (raw == null) continue;
                string s = Regex.Replace(raw.Replace('\t', ' '), @"\s+", " ").Trim().Trim('﻿');
                s = Regex.Replace(s, @"^\d{1,3}\s*[\.\)\-:]?\s+", "");          // "1. An", "12) Bình", "3 - Chi"
                if (s.Length == 0 || Regex.IsMatch(s, @"^\d+$")) continue;
                string f = Fold(s);
                if (f.StartsWith("danh sach") || f == "stt" || f == "ho va ten" || f == "ho ten") continue;   // a heading, not a pupil
                if (s.Length > MaxNameLength) s = s.Substring(0, MaxNameLength).TrimEnd();
                list.Add(s);
                if (list.Count >= MaxNames) break;
            }
            return list;
        }

        /// <summary>Reads names from a .txt, .csv or .xlsx file. Problems become an InvalidDataException with a message for the teacher.</summary>
        public static List<string> FromFile(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists) throw new FileNotFoundException("Không tìm thấy tệp.", path);
            if (info.Length > MaxFileBytes) throw new InvalidDataException("Tệp quá lớn để là danh sách lớp.");
            string ext = info.Extension.ToLowerInvariant();
            List<string> names;
            if (ext == ".xlsx" || ext == ".xlsm") names = FromRows(ReadXlsx(path));
            else if (ext == ".xls") throw new InvalidDataException("Tệp .xls (Excel đời cũ) chưa đọc được. Thầy cô mở bằng Excel rồi lưu lại thành .xlsx, hoặc chép cột tên dán vào ô nhập.");
            else if (ext == ".csv") names = FromRows(ReadText(path).Select(SplitCsv).ToList());
            else names = Clean(ReadText(path));
            if (names.Count == 0) throw new InvalidDataException("Không thấy tên nào trong tệp này.");
            return names;
        }

        static List<string> ReadText(string path)
        {
            var bytes = File.ReadAllBytes(path);
            string text;
            try { text = new UTF8Encoding(false, true).GetString(bytes); }
            catch (DecoderFallbackException) { text = Encoding.Default.GetString(bytes); }   // an old file saved in the Windows code page
            return text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None).ToList();
        }

        static string[] SplitCsv(string line)
        {
            var cells = new List<string>();
            var cur = new StringBuilder();
            bool quoted = false;
            char sep = line.Contains(';') && !line.Contains(',') ? ';' : ',';
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else cur.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == sep || c == '\t') { cells.Add(cur.ToString()); cur.Clear(); }
                else cur.Append(c);
            }
            cells.Add(cur.ToString());
            return cells.ToArray();
        }

        static string Fold(string s)
        {
            var d = (s ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in d) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c == 'đ' ? 'd' : c);
            return Regex.Replace(sb.ToString(), @"\s+", " ");
        }

        /// <summary>
        /// Picks the names out of a table, the way Vietnamese class lists are usually laid out: a header row with
        /// "Họ và tên" (or "Họ đệm" + "Tên" in two columns), "STT", dates of birth… Without a header, the column with
        /// the most text (not numbers or dates) is taken.
        /// </summary>
        public static List<string> FromRows(IList<string[]> rows)
        {
            rows = rows.Where(r => r != null && r.Any(c => !string.IsNullOrWhiteSpace(c))).ToList();
            if (rows.Count == 0) return new List<string>();
            int width = rows.Max(r => r.Length);
            Func<string[], int, string> at = (r, i) => i < r.Length && r[i] != null ? r[i].Trim() : "";

            // a header in the first rows: "họ và tên" / "họ tên" / "tên học sinh", or "họ (đệm)" and "tên" side by side
            for (int h = 0; h < Math.Min(8, rows.Count); h++)
            {
                var head = Enumerable.Range(0, width).Select(i => Fold(at(rows[h], i))).ToArray();
                int full = Array.FindIndex(head, c => c == "ho va ten" || c == "ho ten" || c == "ho va ten hoc sinh" || c == "ten hoc sinh" || c == "hoc sinh" || c == "ho va ten hs" || c == "name" || c == "full name");
                int first = Array.FindIndex(head, c => c == "ho" || c == "ho dem" || c == "ho va ten dem" || c == "ho va dem" || c == "ho lot");
                int last = Array.FindIndex(head, c => c == "ten");
                if (full < 0 && last >= 0 && first < 0) full = last;
                if (full < 0 && first < 0) continue;
                var body = rows.Skip(h + 1);
                var lines = first >= 0 && last >= 0
                    ? body.Select(r => (at(r, first) + " " + at(r, last)).Trim())
                    : body.Select(r => at(r, full));
                return Clean(lines.Where(LooksLikeName));
            }

            // no header: the column with the most name-like cells
            int best = -1, bestCount = 0;
            for (int i = 0; i < width; i++)
            {
                int n = rows.Count(r => LooksLikeName(at(r, i)));
                if (n > bestCount) { best = i; bestCount = n; }
            }
            return best < 0 ? new List<string>() : Clean(rows.Select(r => at(r, best)).Where(LooksLikeName));
        }

        static bool LooksLikeName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            if (!s.Any(char.IsLetter)) return false;
            if (Regex.IsMatch(s, @"^\d+([\/\.\-]\d+)*$")) return false;   // numbers, dates
            string f = Fold(s);
            return f != "nam" && f != "nu" && f != "stt" && f != "lop" && !f.StartsWith("danh sach");
        }

        // ---------------- .xlsx ----------------

        /// <summary>The first worksheet of an .xlsx as rows of cell texts (shared strings, inline strings and numbers).</summary>
        public static List<string[]> ReadXlsx(string path)
        {
            try
            {
                using (var zip = ZipFile.OpenRead(path))
                {
                    XNamespace m = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                    XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships";
                    XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

                    var shared = new List<string>();
                    var sst = Xml(zip, "xl/sharedStrings.xml");
                    if (sst != null)
                        foreach (var si in sst.Root.Elements(m + "si"))
                            shared.Add(string.Concat(si.Descendants(m + "t").Select(t => (string)t)));

                    // the first sheet in workbook order (not necessarily sheet1.xml)
                    string sheetPath = "xl/worksheets/sheet1.xml";
                    var book = Xml(zip, "xl/workbook.xml");
                    var bookRels = Xml(zip, "xl/_rels/workbook.xml.rels");
                    if (book != null && bookRels != null)
                    {
                        var firstSheet = book.Root.Descendants(m + "sheet").FirstOrDefault();
                        string id = firstSheet == null ? null : (string)firstSheet.Attribute(r + "id");
                        var target = bookRels.Root.Elements(rel + "Relationship").FirstOrDefault(e => (string)e.Attribute("Id") == id);
                        if (target != null)
                        {
                            string t = ((string)target.Attribute("Target") ?? "").Replace('\\', '/');
                            sheetPath = t.StartsWith("/") ? t.TrimStart('/') : "xl/" + t;
                        }
                    }
                    var sheet = Xml(zip, sheetPath);
                    if (sheet == null) throw new InvalidDataException("Tệp Excel này không có trang tính nào.");

                    var rows = new List<string[]>();
                    foreach (var row in sheet.Root.Descendants(m + "row"))
                    {
                        var cells = new SortedDictionary<int, string>();
                        int next = 0;
                        foreach (var c in row.Elements(m + "c"))
                        {
                            int col = Column((string)c.Attribute("r"));
                            if (col < 0) col = next;
                            next = col + 1;
                            if (col > 200) continue;
                            string type = (string)c.Attribute("t"), v = (string)c.Element(m + "v");
                            string text;
                            if (type == "s") { int i; text = int.TryParse(v, out i) && i >= 0 && i < shared.Count ? shared[i] : ""; }
                            else if (type == "inlineStr") text = string.Concat(c.Descendants(m + "t").Select(t => (string)t));
                            else text = v ?? "";
                            cells[col] = text;
                        }
                        if (cells.Count == 0) continue;
                        var arr = new string[cells.Keys.Max() + 1];
                        foreach (var kv in cells) arr[kv.Key] = kv.Value;
                        rows.Add(arr);
                        if (rows.Count > 2000) break;
                    }
                    return rows;
                }
            }
            catch (InvalidDataException) { throw; }
            catch (Exception e)
            {
                throw new InvalidDataException("Không đọc được tệp Excel này. Thầy cô thử lưu lại bằng Excel thành .xlsx.", e);
            }
        }

        static int Column(string cellRef)
        {
            if (string.IsNullOrEmpty(cellRef)) return -1;
            int col = 0, i = 0;
            for (; i < cellRef.Length && char.IsLetter(cellRef[i]); i++) col = col * 26 + (char.ToUpperInvariant(cellRef[i]) - 'A' + 1);
            return i == 0 ? -1 : col - 1;
        }

        static XDocument Xml(ZipArchive zip, string name)
        {
            var e = zip.GetEntry(name);
            if (e == null) return null;
            using (var s = e.Open())
            using (var reader = XmlReader.Create(s, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                return XDocument.Load(reader);
        }
    }

    /// <summary>Calls names at random. Everyone is called once before anyone is called again.</summary>
    public sealed class NamePicker
    {
        readonly List<string> names;
        readonly List<int> left = new List<int>();
        readonly Random rng;
        public readonly List<string> Called = new List<string>();

        public NamePicker(IEnumerable<string> names, Random rng = null)
        {
            this.names = (names ?? Enumerable.Empty<string>()).ToList();
            this.rng = rng ?? Fair.NewRandom();
            Reset();
        }

        public int Count { get { return names.Count; } }
        public int Left { get { return left.Count; } }
        public IList<string> All { get { return names; } }

        public void Reset() { left.Clear(); left.AddRange(Enumerable.Range(0, names.Count)); Called.Clear(); }

        /// <summary>The next name (a new round starts by itself when everyone has been called); null if the list is empty.</summary>
        public string Next()
        {
            if (names.Count == 0) return null;
            if (left.Count == 0) { left.AddRange(Enumerable.Range(0, names.Count)); Called.Clear(); }
            int i = rng.Next(left.Count);
            string name = names[left[i]];
            left.RemoveAt(i);
            Called.Add(name);
            return name;
        }

        /// <summary>
        /// One turn of the picker: draws the student called now and the reel of names that rolls past before it.
        /// The reel ends with the called student, exactly once, so the name the slot stops on is the one called.
        /// The names rolling past are students still waiting this round (never the called one), with no name
        /// twice in a row. Null (and an empty reel) if the list is empty.
        /// </summary>
        public string Spin(int length, out List<string> reel)
        {
            reel = new List<string>();
            string called = Next();
            if (called == null) return null;
            var pool = left.Select(i => names[i]).Where(n => n != called).Distinct().ToList();
            if (pool.Count < 2) pool = names.Where(n => n != called).Distinct().ToList();   // end of a round: anyone else may roll past
            for (int i = 0; i < length && pool.Count > 0; i++)
            {
                if (pool.Count == 1 && reel.Count > 0) break;   // a class of two: nothing else to show
                int j = rng.Next(pool.Count);
                if (reel.Count > 0 && pool[j] == reel[reel.Count - 1]) j = (j + 1 + rng.Next(pool.Count - 1)) % pool.Count;
                reel.Add(pool[j]);
            }
            reel.Add(called);
            return called;
        }
    }

    /// <summary>Random draws that do not depend on when a card was opened (Random() alone is seeded from the clock).</summary>
    public static class Fair
    {
        public static Random NewRandom()
        {
            var b = new byte[4];
            using (var r = System.Security.Cryptography.RandomNumberGenerator.Create()) r.GetBytes(b);
            return new Random(BitConverter.ToInt32(b, 0));
        }
    }

    public static class Grouping
    {
        /// <summary>Shuffles the names into 'groups' groups whose sizes differ by at most one.</summary>
        public static List<List<string>> IntoGroups(IList<string> names, int groups, Random rng)
        {
            var result = new List<List<string>>();
            if (names == null || names.Count == 0 || groups < 1) return result;
            groups = Math.Min(groups, names.Count);
            var order = names.OrderBy(n => rng.Next()).ToList();
            for (int g = 0; g < groups; g++) result.Add(new List<string>());
            for (int i = 0; i < order.Count; i++) result[i % groups].Add(order[i]);
            return result;
        }

        /// <summary>Groups of about 'size' pupils each (the last ones share out the rest instead of being left alone).</summary>
        public static List<List<string>> BySize(IList<string> names, int size, Random rng)
        {
            if (names == null || names.Count == 0 || size < 1) return new List<List<string>>();
            int groups = Math.Max(1, (int)Math.Round(names.Count / (double)size));
            return IntoGroups(names, groups, rng);
        }
    }

    /// <summary>Teams and their points for "Thi đua". Kept for the session (and in the settings folder) so a lesson can carry on.</summary>
    public sealed class Contest
    {
        public readonly List<string> Teams = new List<string>();
        public readonly List<int> Scores = new List<int>();
        readonly Stack<Tuple<int, int>> history = new Stack<Tuple<int, int>>();
        public const int MaxTeams = 8, Limit = 9999;

        public Contest(IEnumerable<string> teams)
        {
            foreach (var t in teams ?? Enumerable.Empty<string>())
            {
                if (Teams.Count >= MaxTeams) break;
                Teams.Add(string.IsNullOrWhiteSpace(t) ? "Đội " + (Teams.Count + 1) : t.Trim());
                Scores.Add(0);
            }
        }

        public static Contest Numbered(int count, string word)
        {
            count = Math.Max(2, Math.Min(MaxTeams, count));
            return new Contest(Enumerable.Range(1, count).Select(i => word + " " + i));
        }

        public void Add(int team, int delta)
        {
            if (team < 0 || team >= Scores.Count || delta == 0) return;
            int before = Scores[team];
            Scores[team] = Math.Max(-Limit, Math.Min(Limit, before + delta));
            if (Scores[team] != before) history.Push(Tuple.Create(team, Scores[team] - before));
        }

        public bool CanUndo { get { return history.Count > 0; } }
        public int Undo()
        {
            if (history.Count == 0) return -1;
            var last = history.Pop();
            Scores[last.Item1] -= last.Item2;
            return last.Item1;
        }

        public void ResetScores() { for (int i = 0; i < Scores.Count; i++) Scores[i] = 0; history.Clear(); }

        /// <summary>Teams in the lead (more than one on a tie); empty while nobody has a point.</summary>
        public List<int> Leaders()
        {
            if (Scores.Count == 0) return new List<int>();
            int top = Scores.Max();
            if (top <= 0 && Scores.All(s => s == top)) return new List<int>();
            return Enumerable.Range(0, Scores.Count).Where(i => Scores[i] == top).ToList();
        }
    }

    /// <summary>A countdown that keeps time by the clock (pausing and resuming do not lose or gain time).</summary>
    public sealed class Countdown
    {
        double total, left, startedAt = -1;
        public const double MaxSeconds = 99 * 60 + 59;

        public Countdown(double seconds) { Set(seconds); }

        public double Total { get { return total; } }
        public bool Running { get { return startedAt >= 0; } }

        public void Set(double seconds)
        {
            total = left = Math.Max(1, Math.Min(MaxSeconds, seconds));
            startedAt = -1;
        }

        public double Remaining(double now) { return Running ? Math.Max(0, left - (now - startedAt)) : left; }
        public bool Ended(double now) { return Remaining(now) <= 0; }

        public void Start(double now) { if (!Running && left > 0) startedAt = now; }
        public void Pause(double now) { if (!Running) return; left = Remaining(now); startedAt = -1; }
        public void Reset() { left = total; startedAt = -1; }

        /// <summary>Adds (or with a negative value takes away) time, also while running.</summary>
        public void AddTime(double seconds, double now)
        {
            double r = Math.Max(0, Math.Min(MaxSeconds, Remaining(now) + seconds));
            if (Running) startedAt = now;
            left = r;
            total = Math.Max(total, r);
        }

        public static string Format(double seconds)
        {
            int s = (int)Math.Ceiling(Math.Max(0, seconds));
            return (s / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (s % 60).ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
