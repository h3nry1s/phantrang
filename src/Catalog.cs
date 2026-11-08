using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PhanTrang
{
    /// <summary>Colours of the Phấn Trắng look: chalkboard green, chalk, cream paper, purple ink, red pen.</summary>
    public static class Theme
    {
        public static readonly Color Paper = Color.FromArgb(251, 248, 239);
        public static readonly Color Cream = Color.FromArgb(246, 242, 231);
        public static readonly Color Sidebar = Color.FromArgb(239, 233, 218);
        public static readonly Color Line = Color.FromArgb(227, 218, 196);
        public static readonly Color LineStrong = Color.FromArgb(207, 197, 172);
        public static readonly Color Hover = Color.FromArgb(230, 222, 200);
        public static readonly Color Tray = Color.FromArgb(239, 233, 218);
        public static readonly Color Green = Color.FromArgb(30, 74, 59);
        public static readonly Color GreenSoft = Color.FromArgb(227, 239, 232);
        public static readonly Color Board = Color.FromArgb(36, 83, 63);
        public static readonly Color Ink = Color.FromArgb(31, 29, 25);
        public static readonly Color Text = Color.FromArgb(62, 58, 49);
        public static readonly Color Dim = Color.FromArgb(107, 100, 85);
        public static readonly Color Red = Color.FromArgb(198, 40, 40);
        public static readonly Color Purple = Color.FromArgb(90, 62, 158);
        public static readonly Color Yellow = Color.FromArgb(242, 194, 48);
        public static readonly Color Chalk = Color.FromArgb(244, 241, 230);
        public static readonly Color Wood = Color.FromArgb(217, 169, 91);
        public static readonly Color WoodDark = Color.FromArgb(138, 95, 36);
        public static readonly Color Frame = Color.FromArgb(110, 82, 52);
    }

    /// <summary>The bundled OFL fonts (assets\fonts). Each file is its own collection so a face is never mixed up.</summary>
    public static class Fonts
    {
        static readonly Dictionary<string, FontFamily> families = new Dictionary<string, FontFamily>();
        static readonly List<PrivateFontCollection> keep = new List<PrivateFontCollection>();
        static bool loaded;

        static readonly string[][] Files =
        {
            new[] { "ui", "BeVietnamPro-Regular.ttf" },
            new[] { "uiSemi", "BeVietnamPro-SemiBold.ttf" },
            new[] { "uiBold", "BeVietnamPro-Bold.ttf" },
            new[] { "chalk", "PatrickHand-Regular.ttf" },
            new[] { "mali", "Mali-SemiBold.ttf" },
            new[] { "slant", "DancingScript.ttf" },
            new[] { "brush", "Charm-Bold.ttf" },
            new[] { "print", "BeVietnamPro-Bold.ttf" },
        };

        [System.Runtime.InteropServices.DllImport("gdi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int AddFontResourceEx(string path, uint flags, IntPtr reserved);

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            string dir = Path.Combine(Catalog.AssetsDir, "fonts");
            foreach (var f in Files)
            {
                try
                {
                    string path = Path.Combine(dir, f[1]);
                    if (!File.Exists(path)) continue;
                    // GDI (text boxes, combo boxes, menus) sees the font only through AddFontResourceEx;
                    // GDI+ (everything we paint) through the private collection.
                    AddFontResourceEx(path, 0x10 /* FR_PRIVATE */, IntPtr.Zero);
                    var c = new PrivateFontCollection();
                    c.AddFontFile(path);
                    keep.Add(c);
                    families[f[0]] = c.Families[0];
                }
                catch { }
            }
        }

        public static FontFamily Family(string id)
        {
            Load();
            FontFamily f;
            if (families.TryGetValue(id, out f)) return f;
            return new FontFamily(id == "slant" || id == "brush" ? "Segoe Script" : "Segoe UI");
        }

        public static FontStyle StyleFor(FontFamily f)
        {
            if (f.IsStyleAvailable(FontStyle.Regular)) return FontStyle.Regular;
            if (f.IsStyleAvailable(FontStyle.Bold)) return FontStyle.Bold;
            return FontStyle.Italic;
        }

        /// <summary>A font in pixel units. Callers dispose it.</summary>
        public static Font Get(string id, float px)
        {
            var fam = Family(id);
            // The Bold file shares the family name of the Regular one; GDI (window text) only picks it when asked for Bold.
            var style = (id == "uiBold" || id == "print") && fam.IsStyleAvailable(FontStyle.Bold) ? FontStyle.Bold : StyleFor(fam);
            return new Font(fam, px, style, GraphicsUnit.Pixel);
        }

        // ---- glyph coverage, so maths symbols missing from a handwriting font fall back to a symbol font ----

        [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern uint GetFontUnicodeRanges(IntPtr hdc, IntPtr glyphset);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);

        static readonly Dictionary<string, List<int[]>> ranges = new Dictionary<string, List<int[]>>();

        /// <summary>True when the family has a glyph for c (asked from GDI once per family).</summary>
        public static bool Covers(FontFamily fam, char c)
        {
            if (c < 0x80 || char.IsWhiteSpace(c) || char.IsSurrogate(c)) return true;
            List<int[]> list;
            if (!ranges.TryGetValue(fam.Name, out list))
            {
                list = new List<int[]>();
                try
                {
                    using (var f = new Font(fam, 32, StyleFor(fam), GraphicsUnit.Pixel))
                    {
                        IntPtr dc = CreateCompatibleDC(IntPtr.Zero), hf = f.ToHfont(), old = SelectObject(dc, hf);
                        uint size = GetFontUnicodeRanges(dc, IntPtr.Zero);
                        if (size > 0)
                        {
                            IntPtr buf = System.Runtime.InteropServices.Marshal.AllocHGlobal((int)size);
                            GetFontUnicodeRanges(dc, buf);
                            int count = System.Runtime.InteropServices.Marshal.ReadInt32(buf, 12);
                            for (int i = 0; i < count; i++)
                            {
                                int low = (ushort)System.Runtime.InteropServices.Marshal.ReadInt16(buf, 16 + i * 4);
                                int n = (ushort)System.Runtime.InteropServices.Marshal.ReadInt16(buf, 18 + i * 4);
                                list.Add(new[] { low, low + n - 1 });
                            }
                            System.Runtime.InteropServices.Marshal.FreeHGlobal(buf);
                        }
                        SelectObject(dc, old);
                        DeleteObject(hf);
                        DeleteDC(dc);
                    }
                }
                catch { }
                ranges[fam.Name] = list;
            }
            if (list.Count == 0) return true;   // unknown: trust the font
            foreach (var r in list) if (c >= r[0] && c <= r[1]) return true;
            return false;
        }

        static FontFamily[] fallbacks;
        /// <summary>Symbol fonts that ship with Windows, tried in order for characters the main font lacks.</summary>
        public static FontFamily[] Fallbacks
        {
            get
            {
                if (fallbacks == null)
                {
                    var list = new List<FontFamily>();
                    foreach (var n in new[] { "Segoe UI Symbol", "Cambria Math", "Segoe UI" })
                        try { list.Add(new FontFamily(n)); } catch { }
                    fallbacks = list.ToArray();
                }
                return fallbacks;
            }
        }
    }

    public static class Catalog
    {
        public static readonly string[][] Pointers =
        {
            new[] { "chalk", "Viên phấn" }, new[] { "ruler", "Thước gỗ" }, new[] { "pen", "Bút mực tím" }, new[] { "laser", "Chấm laser" },
        };

        public static readonly string[][] Inks =
        {
            new[] { "red", "Bút đỏ", "C62828" }, new[] { "white", "Phấn trắng", "F4F1E6" }, new[] { "yellow", "Phấn vàng", "F2C230" },
            new[] { "purple", "Mực tím", "5A3E9E" }, new[] { "blue", "Xanh dương", "1E5AA8" }, new[] { "pink", "Phấn hồng", "F08080" },
            new[] { "black", "Mực đen", "22201C" },
        };

        public static readonly string[][] Shapes =
        {
            new[] { "line", "Đường thẳng" }, new[] { "arrow", "Mũi tên" }, new[] { "rect", "Khung" }, new[] { "ellipse", "Hình tròn" },
        };

        public static readonly string[][] Stamps =
        {
            new[] { "diem10", "Điểm 10" }, new[] { "hoa", "Hoa điểm tốt" }, new[] { "totlam", "Tốt lắm!" },
            new[] { "dung", "Đúng" }, new[] { "sai", "Sai" }, new[] { "sao", "Ngôi sao" },
        };

        public static readonly string[][] Widths =
        {
            new[] { "thin", "Nét mảnh", "0.6" }, new[] { "medium", "Nét vừa", "1" }, new[] { "thick", "Nét đậm", "1.7" },
        };

        public static readonly string[][] Boards =
        {
            new[] { "none", "Tắt bảng" }, new[] { "green", "Bảng xanh" }, new[] { "black", "Bảng đen" }, new[] { "white", "Bảng trắng" },
            new[] { "oli", "Vở ô li" }, new[] { "grid", "Giấy ô vuông" }, new[] { "lines", "Giấy kẻ dòng" },
        };

        public static readonly string[][] TextFonts =
        {
            new[] { "chalk", "Chữ phấn" }, new[] { "mali", "Tròn tiểu học" }, new[] { "slant", "Nét nghiêng" },
            new[] { "brush", "Bút lông" }, new[] { "print", "Chữ in rõ" },
        };

        /// <summary>Text height as a fraction of the screen height.</summary>
        public static readonly string[][] TextSizes =
        {
            new[] { "s", "Nhỏ", "0.034" }, new[] { "m", "Vừa", "0.048" }, new[] { "l", "To", "0.064" }, new[] { "xl", "Rất to", "0.085" },
        };

        public static readonly string[][] PointerSizes =
        {
            new[] { "0.09", "Nhỏ" }, new[] { "0.13", "Vừa" }, new[] { "0.17", "Lớn" }, new[] { "0.21", "Rất lớn" },
        };

        /// <summary>The entry of 'list' whose id (first column) is 'id', or null.</summary>
        static string[] Find(string[][] list, string id) { return Array.Find(list, row => row[0] == id); }

        /// <summary>The display name for an id, or the id itself if unknown.</summary>
        public static string Label(string[][] list, string id) { var row = Find(list, id); return row != null ? row[1] : id; }

        /// <summary>Third column of the entry for 'id' as a number, or 'fallback'.</summary>
        static double Third(string[][] list, string id, double fallback)
        {
            var row = Find(list, id);
            double v;
            return row != null && double.TryParse(row[2], NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        /// <summary>"RRGGBB" to an opaque colour.</summary>
        public static Color Hex(string rgb)
        {
            int v = int.Parse(rgb, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return Color.FromArgb((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
        }

        public static Color InkColor { get { return Hex((Find(Inks, Settings.InkChoice) ?? Inks[0])[2]); } }

        /// <summary>Chalkboards: dark ink would not show on them.</summary>
        public static bool IsDarkBoard(string board) { return board == "green" || board == "black"; }

        /// <summary>Ink that stays readable: dark ink turns to chalk on a dark board, chalk turns dark on paper.</summary>
        public static Color Readable(Color c, string board)
        {
            int brightness = (c.R * 299 + c.G * 587 + c.B * 114) / 1000;   // perceived lightness, 0–255
            if (IsDarkBoard(board)) return brightness < 70 ? Theme.Chalk : c;
            return board != "none" && brightness > 225 ? Theme.Ink : c;
        }

        public static double WidthFactor { get { return Third(Widths, Settings.PenWidth, 1); } }

        public static double TextSizeFraction { get { return Third(TextSizes, Settings.TextSize, 0.048); } }

        /// <summary>The assets folder next to the exe (or one level up, when running from a build folder).</summary>
        public static string AssetsDir
        {
            get
            {
                // tools that draw with the app's code from outside it (Store screenshots, tests) say where the assets are
                string given = Environment.GetEnvironmentVariable("PHANTRANG_ASSETS");
                if (!string.IsNullOrEmpty(given) && Directory.Exists(given)) return Path.GetFullPath(given);
                string home = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
                string beside = Path.Combine(home, "assets"), above = Path.GetFullPath(Path.Combine(home, "..", "assets"));
                return Directory.Exists(beside) || !Directory.Exists(above) ? beside : above;
            }
        }
    }
}
