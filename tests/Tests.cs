// Phấn Trắng self-tests: a small runner (no test framework needed, built with Windows' csc like the app).
// Run: powershell -File tests\run-tests.ps1
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using PhanTrang;

static class T
{
    static int pass, fail;
    static readonly List<string> failures = new List<string>();

    static void Check(bool ok, string what) { if (!ok) throw new Exception("kiểm tra sai: " + what); }
    static void Eq<V>(V want, V got, string what) { if (!Equals(want, got)) throw new Exception(what + ": cần " + want + ", được " + got); }
    static void Near(double want, double got, double tol, string what) { if (Math.Abs(want - got) > tol) throw new Exception(what + ": cần " + want + ", được " + got); }
    static void Throws<E>(Action a, string what) where E : Exception
    {
        try { a(); } catch (E) { return; } catch (Exception e) { throw new Exception(what + ": ném " + e.GetType().Name + " thay vì " + typeof(E).Name); }
        throw new Exception(what + ": không ném " + typeof(E).Name);
    }

    static void Run(string name, Action test)
    {
        try { test(); pass++; Console.WriteLine("  ĐẠT   " + name); }
        catch (Exception e) { fail++; failures.Add(name + " — " + e.Message); Console.WriteLine("  LỖI   " + name + "\n         " + e.Message); }
    }

    static string Tmp;

    /// <summary>A minimal .xlsx: shared strings and one sheet with the given &lt;row&gt; elements.</summary>
    static void WriteXlsx(string path, string[] shared, string rows)
    {
        if (File.Exists(path)) File.Delete(path);
        using (var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create))
        {
            Action<string, string> put = (name, text) => { using (var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false))) w.Write(text); };
            const string M = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            put("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"/>");
            put("xl/workbook.xml", "<workbook xmlns=\"" + M + "\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Lớp\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            put("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"x\" Target=\"worksheets/lop.xml\"/></Relationships>");
            put("xl/sharedStrings.xml", "<sst xmlns=\"" + M + "\">" + string.Concat(shared.Select(s => "<si><t>" + System.Security.SecurityElement.Escape(s) + "</t></si>")) + "</sst>");
            put("xl/worksheets/lop.xml", "<worksheet xmlns=\"" + M + "\"><sheetData>" + rows + "</sheetData></worksheet>");
        }
    }

    static string Part(System.IO.Compression.ZipArchive zip, string name)
    {
        using (var s = zip.GetEntry(name).Open()) using (var r = new StreamReader(s, Encoding.UTF8)) return r.ReadToEnd();
    }

    // ---------------- helpers ----------------

    static TextMark Text(string s, float x, float y, float px = 52)
    {
        var t = new TextMark { Text = s, FontId = "mali", Px = px, Pos = new PointF(x, y), Color = Color.White };
        t.Layout();
        return t;
    }

    static PenMark Stroke(int n, float x0, float y0)
    {
        var s = new PenMark { Width = 6, Color = Color.Yellow };
        s.AddRange(Enumerable.Range(0, n).Select(i => new PointF(x0 + i * 5, y0 + (float)Math.Sin(i / 3.0) * 20)));
        return s;
    }

    static byte[] Png(int w, int h)
    {
        using (var b = new Bitmap(w, h))
        using (var g = Graphics.FromImage(b))
        using (var ms = new MemoryStream())
        {
            g.Clear(Color.SkyBlue);
            g.FillEllipse(Brushes.Red, w / 4, h / 4, w / 2, h / 2);
            b.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }

    static byte[] Jpg(int w, int h)
    {
        using (var b = new Bitmap(w, h))
        using (var g = Graphics.FromImage(b))
        using (var ms = new MemoryStream())
        {
            g.Clear(Color.Beige);
            b.Save(ms, ImageFormat.Jpeg);
            return ms.ToArray();
        }
    }

    static List<Sheet> Sample()
    {
        var p1 = new Sheet { Board = "green" };
        p1.Marks.Add(Text("Bài 5: √16 = 4, x² ≥ 0", 130, 86));
        p1.Marks.Add(Stroke(40, 300, 400));
        var p2 = new Sheet { Board = "oli" };
        var sh = new ShapeMark { Kind = "arrow", Width = 6, Color = Color.Red, P0 = new PointF(200, 200) };
        sh.SetEnd(new PointF(600, 300));
        p2.Marks.Add(sh);
        var st = new StampMark { Kind = "diem10", Size = 92, Center = new PointF(900, 500) };
        st.Layout();
        p2.Marks.Add(st);
        var gone = Stroke(5, 10, 10); gone.Temporary = true;
        p2.Marks.Add(gone);
        return new List<Sheet> { p1, p2 };
    }

    static string Doc(string pagesJson) { return "{\"app\":\"PhanTrang\",\"version\":1,\"aspect\":1.77778,\"pages\":" + pagesJson + "}"; }

    // ---------------- tests ----------------

    static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Tmp = Path.Combine(Path.GetTempPath(), "phantrang-tests-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(Tmp);
        Fonts.Load();
        Console.WriteLine("Phấn Trắng – kiểm thử tự động\n");

        Console.WriteLine("Gõ tắt ký hiệu toán");
        Run("^2 thành ²", () => { string r; int c; Check(MathInput.Apply("x^2", 3, out r, out c), "đổi"); Eq("x²", r, "chữ"); Eq(2, c, "con trỏ"); });
        Run(">= và <= thành ≥ ≤", () => { string r; int c; MathInput.Apply("a>=", 3, out r, out c); Eq("a≥", r, ">="); MathInput.Apply("b<=", 3, out r, out c); Eq("b≤", r, "<="); });
        Run("≤ rồi > thành ⇔", () => { string r; int c; MathInput.Apply("≤>", 2, out r, out c); Eq("⇔", r, "chữ"); });
        Run("sqrt thành √, _1 thành ₁, * thành ×", () =>
        {
            string r; int c;
            MathInput.Apply("sqrt", 4, out r, out c); Eq("√", r, "sqrt");
            MathInput.Apply("x_1", 3, out r, out c); Eq("x₁", r, "_1");
            MathInput.Apply("2*", 2, out r, out c); Eq("2×", r, "*");
        });
        Run("đổi ở giữa dòng, giữ phần sau con trỏ", () => { string r; int c; MathInput.Apply("x^2 + y", 3, out r, out c); Eq("x² + y", r, "chữ"); Eq(2, c, "con trỏ"); });
        Run("không có gì để đổi / con trỏ sai thì để nguyên", () =>
        {
            string r; int c;
            Check(!MathInput.Apply("bình thường", 11, out r, out c), "chữ thường");
            Check(!MathInput.Apply("", 0, out r, out c), "rỗng");
            Check(!MathInput.Apply("x^2", 99, out r, out c), "con trỏ ngoài chuỗi");
            Check(!MathInput.Apply("x^2", 0, out r, out c), "con trỏ ở đầu");
        });

        Console.WriteLine("\nMở PowerPoint: khung trang chiếu");
        Run("slide 16:9 vừa khít màn 16:9", () => { var r = PptxImport.Fit(1600, 900, 1920, 1080); Check(Math.Abs(r.X) < 0.01 && Math.Abs(r.Y) < 0.01, "không có lề"); Eq(1920.0, Math.Round(r.Width, 2), "rộng"); Eq(1080.0, Math.Round(r.Height, 2), "cao"); });
        Run("slide 4:3 trên màn 16:9: cao đủ, căn giữa ngang", () => { var r = PptxImport.Fit(1024, 768, 1920, 1080); Eq(1080f, r.Height, "cao"); Eq(1440f, r.Width, "rộng"); Eq(240f, r.X, "lề trái"); Eq(0f, r.Y, "lề trên"); });
        Run("slide 16:9 trên máy chiếu 4:3: rộng đủ, căn giữa dọc", () => { var r = PptxImport.Fit(1600, 900, 1024, 768); Eq(1024f, r.Width, "rộng"); Eq(576f, r.Height, "cao"); Eq(96f, r.Y, "lề trên"); });

        Console.WriteLine("\nBài giảng: lưu và mở");
        Run("lưu rồi mở lại cùng cỡ màn hình", () =>
        {
            string f = Path.Combine(Tmp, "a.phantrang");
            Lesson.Save(f, Sample(), 1920, 1080, "green");
            var back = Lesson.Load(f, 1920, 1080);
            Eq(2, back.Count, "số trang");
            Eq(2, back[0].Marks.Count, "trang 1");
            Eq(2, back[1].Marks.Count, "trang 2 (nét vẽ tạm không được lưu)");
            var t = (TextMark)back[0].Marks[0];
            Eq("Bài 5: √16 = 4, x² ≥ 0", t.Text, "chữ");
            Near(130, t.Pos.X, 0.5, "x"); Near(86, t.Pos.Y, 0.5, "y"); Near(52, t.Px, 0.1, "cỡ chữ");
            Eq(40, ((PenMark)back[0].Marks[1]).Pts.Count, "số điểm nét vẽ");
        });
        Run("mỗi trang giữ loại bảng riêng", () =>
        {
            string f = Path.Combine(Tmp, "b.phantrang");
            Lesson.Save(f, Sample(), 1920, 1080, "green");
            var back = Lesson.Load(f, 1920, 1080);
            Eq("green", back[0].Board, "trang 1"); Eq("oli", back[1].Board, "trang 2");
        });
        Run("mở trên máy chiếu nhỏ hơn: đúng tỉ lệ", () =>
        {
            string f = Path.Combine(Tmp, "c.phantrang");
            Lesson.Save(f, Sample(), 1920, 1080, "green");
            var back = Lesson.Load(f, 1366, 768);
            var st = (StampMark)back[1].Marks[1];
            double k = 768 / 1080.0, off = (1366 - 1920 * k) / 2;
            Near(900 * k + off, st.Center.X, 1, "x"); Near(500 * k, st.Center.Y, 1, "y"); Near(92 * k, st.Size, 0.5, "cỡ dấu");
        });
        Run("mở trên màn 4:3: nội dung được canh giữa", () =>
        {
            string f = Path.Combine(Tmp, "d.phantrang");
            Lesson.Save(f, Sample(), 1920, 1080, "green");
            var back = Lesson.Load(f, 1024, 768);
            var t = (TextMark)back[0].Marks[0];
            double k = 768 / 1080.0, off = (1024 - 1920 * k) / 2;
            Near(130 * k + off, t.Pos.X, 1, "x");
        });
        Run("lưu đè bài cũ không để lại tệp tạm", () =>
        {
            string f = Path.Combine(Tmp, "e.phantrang");
            Lesson.Save(f, Sample(), 1920, 1080, "green");
            var one = new List<Sheet> { new Sheet { Board = "white" } }; one[0].Marks.Add(Text("mới", 10, 10));
            Lesson.Save(f, one, 1920, 1080, "green");
            Eq(1, Lesson.Load(f, 1920, 1080).Count, "số trang sau khi lưu đè");
            Check(!File.Exists(f + ".tmp"), "còn tệp .tmp");
        });
        Run("xuất mỗi trang thành một ảnh PNG", () =>
        {
            int n = Lesson.ExportImages(Path.Combine(Tmp, "bang.png"), Sample(), 800, 450, "green");
            Eq(2, n, "số ảnh");
            using (var img = Image.FromFile(Path.Combine(Tmp, "bang-2.png"))) { Eq(800, img.Width, "rộng"); Eq(450, img.Height, "cao"); }
        });

        Console.WriteLine("\nBài giảng: tệp hỏng hoặc độc hại (tệp gửi qua USB, Zalo)");
        Run("không phải JSON → báo lỗi rõ ràng", () => Throws<InvalidDataException>(() => Lesson.Parse("đây không phải bài giảng", 1920, 1080), "chữ thường"));
        Run("thiếu pages / pages sai kiểu / không có trang", () =>
        {
            Throws<InvalidDataException>(() => Lesson.Parse("{\"app\":\"x\"}", 1920, 1080), "thiếu pages");
            Throws<InvalidDataException>(() => Lesson.Parse("{\"pages\":\"abc\"}", 1920, 1080), "pages là chuỗi");
            Throws<InvalidDataException>(() => Lesson.Parse("{\"pages\":[]}", 1920, 1080), "pages rỗng");
            Throws<InvalidDataException>(() => Lesson.Parse("[1,2,3]", 1920, 1080), "gốc là mảng");
        });
        Run("JSON lồng quá sâu bị từ chối", () =>
            Throws<InvalidDataException>(() => Lesson.Parse(new string('[', 5000) + new string(']', 5000), 1920, 1080), "lồng 5000 tầng"));
        Run("quá nhiều trang bị từ chối", () =>
        {
            var pages = string.Join(",", Enumerable.Repeat("{\"items\":[]}", Lesson.MaxPages + 1).ToArray());
            Throws<InvalidDataException>(() => Lesson.Parse(Doc("[" + pages + "]"), 1920, 1080), "301 trang");
        });
        Run("số vô lý (NaN, rất lớn, chữ) chỉ bỏ nét đó, phần còn lại vẫn mở", () =>
        {
            string items = "[" +
                "{\"t\":\"text\",\"s\":\"ok\",\"f\":\"mali\",\"c\":\"FFFFFFFF\",\"px\":0.05,\"x\":0.1,\"y\":0.1}," +
                "{\"t\":\"text\",\"s\":\"to\",\"f\":\"mali\",\"c\":\"FFFFFFFF\",\"px\":1e300,\"x\":0.1,\"y\":0.1}," +
                "{\"t\":\"stamp\",\"k\":\"diem10\",\"x\":\"NaN\",\"y\":0.2,\"size\":0.08}," +
                "{\"t\":\"stroke\",\"c\":\"FFFF0000\",\"w\":0.005,\"pts\":[0.1,0.1,1e40,0.2]}," +
                "{\"t\":\"shape\",\"k\":\"arrow\",\"c\":\"FFFF0000\",\"w\":-5,\"p\":[0,0,1,1]}" + "]";
            var back = Lesson.Parse(Doc("[{\"board\":\"green\",\"items\":" + items + "}]"), 1920, 1080);
            Eq(1, back[0].Marks.Count, "chỉ còn nét hợp lệ");
        });
        Run("loại dấu, hình, phông, bảng lạ bị bỏ qua hoặc đổi về mặc định", () =>
        {
            string items = "[{\"t\":\"stamp\",\"k\":\"hacker\",\"x\":0.1,\"y\":0.1,\"size\":0.08},{\"t\":\"shape\",\"k\":\"virus\",\"c\":\"FFFF0000\",\"w\":0.005,\"p\":[0,0,1,1]}," +
                "{\"t\":\"text\",\"s\":\"x\",\"f\":\"C:\\\\Windows\\\\evil.ttf\",\"c\":\"FFFFFFFF\",\"px\":0.05,\"x\":0.1,\"y\":0.1},{\"t\":\"exe\",\"run\":\"calc\"}]";
            var back = Lesson.Parse(Doc("[{\"board\":\"..\\\\..\\\\x\",\"items\":" + items + "}]"), 1920, 1080);
            Eq(1, back[0].Marks.Count, "chỉ còn dòng chữ");
            Eq("mali", ((TextMark)back[0].Marks[0]).FontId, "phông lạ đổi về mặc định");
            Eq(null, back[0].Board, "bảng lạ bị bỏ");
        });
        Run("chữ quá dài bị cắt bớt, màu hỏng thành màu mặc định", () =>
        {
            string longText = new string('a', 10000);
            var back = Lesson.Parse(Doc("[{\"items\":[{\"t\":\"text\",\"s\":\"" + longText + "\",\"f\":\"mali\",\"c\":\"xyz\",\"px\":0.05,\"x\":0.1,\"y\":0.1}]}]"), 1920, 1080);
            var t = (TextMark)back[0].Marks[0];
            Eq(Lesson.MaxTextLength, t.Text.Length, "độ dài");
            Eq(Theme.Red.ToArgb(), t.Color.ToArgb(), "màu");
        });
        Run("mở tệp không tồn tại → FileNotFoundException", () => Throws<FileNotFoundException>(() => Lesson.Load(Path.Combine(Tmp, "khong-co.phantrang"), 1920, 1080), "thiếu tệp"));

        Console.WriteLine("\nẢnh trong bài giảng");
        Run("ảnh to được thu về 1600px, giữ tỉ lệ", () =>
        {
            var im = PictureMark.FromBytes(Png(3200, 1600));
            Eq(1600, im.Img.Width, "rộng"); Eq(800, im.Img.Height, "cao");
        });
        Run("ảnh nhỏ giữ nguyên cỡ; JPEG lưu lại là JPEG, PNG là PNG", () =>
        {
            var j = PictureMark.FromBytes(Jpg(300, 200));
            Eq(300, j.Img.Width, "rộng");
            Check(j.Data[0] == 0xFF && j.Data[1] == 0xD8, "JPEG");
            var p = PictureMark.FromBytes(Png(64, 64));
            Check(p.Data[0] == 0x89 && p.Data[1] == 0x50, "PNG");
        });
        Run("tệp không phải ảnh (đổi đuôi .png) bị từ chối", () =>
        {
            Throws<InvalidDataException>(() => PictureMark.FromBytes(Encoding.UTF8.GetBytes("MZ fake exe renamed to picture.png")), "tệp chữ");
            Throws<InvalidDataException>(() => PictureMark.FromBytes(new byte[0]), "rỗng");
            Throws<InvalidDataException>(() => PictureMark.FromBytes(null), "null");
            var broken = Png(50, 50); Array.Resize(ref broken, 40);
            Throws<InvalidDataException>(() => PictureMark.FromBytes(broken), "PNG cụt");
        });
        Run("ảnh được lưu trong bài và mở lại đúng chỗ", () =>
        {
            var im = PictureMark.FromBytes(Png(400, 300));
            im.PlaceAt(new PointF(960, 540), 1000, 600);
            var pages = new List<Sheet> { new Sheet { Board = "white" } };
            pages[0].Marks.Add(im);
            string f = Path.Combine(Tmp, "anh.phantrang");
            Lesson.Save(f, pages, 1920, 1080, "white");
            var back = Lesson.Load(f, 1920, 1080);
            var b = back[0].Marks.OfType<PictureMark>().Single();
            Near(im.Rect.X, b.Rect.X, 0.5, "x"); Near(im.Rect.Width, b.Rect.Width, 0.5, "rộng");
            Eq(400, b.Img.Width, "điểm ảnh");
        });
        Run("ảnh hỏng bên trong bài giảng chỉ bỏ ảnh đó", () =>
        {
            string items = "[{\"t\":\"image\",\"data\":\"" + Convert.ToBase64String(Encoding.UTF8.GetBytes("not an image")) + "\",\"x\":0.1,\"y\":0.1,\"w\":0.3,\"h\":0.2}," +
                           "{\"t\":\"image\",\"data\":\"@@@ không phải base64 @@@\",\"x\":0.1,\"y\":0.1,\"w\":0.3,\"h\":0.2}," +
                           "{\"t\":\"stamp\",\"k\":\"sao\",\"x\":0.5,\"y\":0.5,\"size\":0.08}]";
            Eq(1, Lesson.Parse(Doc("[{\"items\":" + items + "}]"), 1920, 1080)[0].Marks.Count, "còn lại dấu sao");
        });
        Run("đặt ảnh: vừa khung, bấm là đặt, Hoàn tác lấy ra được", () =>
        {
            var sc = new Stage(); sc.Resize(1920, 1080); sc.RefH = 1080;
            var im = PictureMark.FromBytes(Png(1600, 400));
            sc.Pictures.Begin(im, PointF.Empty);
            Check(sc.Pictures.Placing, "đang đặt");
            sc.Pictures.Drop(new PointF(960, 540));
            Check(!sc.Pictures.Placing, "đã đặt");
            Eq(1, sc.Current.Marks.Count, "có trên trang");
            Check(im.Rect.Width <= 1920 * 0.55f + 0.5f, "không rộng quá khung");
            sc.Undo();
            Eq(0, sc.Current.Marks.Count, "Hoàn tác");
        });

        Run("kéo thân ảnh để di chuyển, Hoàn tác trả về chỗ cũ", () =>
        {
            var sc = new Stage(); sc.Resize(1920, 1080); sc.RefH = 1080; sc.SetBoard("white");
            var im = PictureMark.FromBytes(Png(400, 300));
            sc.Pictures.Begin(im, PointF.Empty); sc.Pictures.Drop(new PointF(500, 500));
            sc.Pictures.Grab(new PointF(510, 505)); Eq("move", sc.Pictures.Grip, "bấm vào thân ảnh");
            sc.Pictures.Drag(new PointF(810, 405), false);
            sc.EndAll();
            Near(800, im.Center.X, 0.5, "x mới"); Near(400, im.Center.Y, 0.5, "y mới");
            sc.Undo(); Near(500, im.Center.X, 0.5, "x sau hoàn tác");
            sc.Redo(); Near(800, im.Center.X, 0.5, "x sau làm lại");
        });
        Run("kéo nút tròn để xoay (Shift: nấc 15°), kéo góc để phóng to", () =>
        {
            var sc = new Stage(); sc.Resize(1920, 1080); sc.RefH = 1080; sc.SetBoard("white");
            var im = PictureMark.FromBytes(Png(400, 200));
            sc.Pictures.Begin(im, PointF.Empty); sc.Pictures.Drop(new PointF(960, 540));
            float hh = im.Size.Height / 2;
            var handle = new PointF(960, 540 - hh - (float)Math.Max(28, 1080 * 0.04));
            sc.Pictures.Grab(handle); Eq("rotate", sc.Pictures.Grip, "nút xoay");
            sc.Pictures.Drag(new PointF(960 + 300, 540 - 290), true);   // ≈ 46° → 45° with Shift
            sc.EndAll();
            Near(45, im.Angle, 0.01, "góc");
            sc.Undo(); Near(0, im.Angle, 0.01, "góc sau hoàn tác");
            var corner = new PointF(960 + im.Size.Width / 2, 540 + hh);
            sc.Pictures.Grab(corner); Eq("scale", sc.Pictures.Grip, "góc ảnh");
            float w0 = im.Size.Width;
            sc.Pictures.Drag(new PointF(960 + (corner.X - 960) * 2, 540 + (corner.Y - 540) * 2), false);
            sc.EndAll();
            Near(w0 * 2, im.Size.Width, 1, "rộng gấp đôi");
            Near(im.Size.Width / im.Size.Height, 2, 0.01, "giữ tỉ lệ");
        });
        Run("ảnh đã xoay: bấm trúng theo hình xoay, không theo khung cũ", () =>
        {
            var im = PictureMark.FromBytes(Png(400, 40));
            im.Rect = new RectangleF(300, 480, 400, 40);   // centre 500,500
            im.Angle = 90;                                  // now tall and thin
            Check(im.Hit(new PointF(500, 680), 0), "trong ảnh sau khi xoay");
            Check(!im.Hit(new PointF(680, 500), 0), "chỗ cũ không còn là ảnh");
        });
        Run("góc xoay được lưu trong bài và mở lại", () =>
        {
            var im = PictureMark.FromBytes(Png(300, 200));
            im.Rect = new RectangleF(100, 100, 300, 200); im.Angle = 30;
            var pages = new List<Sheet> { new Sheet { Board = "green" } }; pages[0].Marks.Add(im);
            string f = Path.Combine(Tmp, "xoay.phantrang");
            Lesson.Save(f, pages, 1920, 1080, "green");
            var b = Lesson.Load(f, 1920, 1080)[0].Marks.OfType<PictureMark>().Single();
            Near(30, b.Angle, 0.01, "góc"); Near(250, b.Center.X, 0.5, "tâm");
        });
        Run("xoay 90° và xoá ảnh đang chọn, đều hoàn tác được", () =>
        {
            var sc = new Stage(); sc.Resize(1920, 1080); sc.RefH = 1080; sc.SetBoard("white");
            var im = PictureMark.FromBytes(Png(200, 100));
            sc.Pictures.Begin(im, PointF.Empty); sc.Pictures.Drop(new PointF(400, 400));
            sc.Pictures.Turn(-90); Near(270, im.Angle, 0.01, "xoay trái");
            sc.Pictures.Delete(); Eq(0, sc.Current.Marks.Count, "đã xoá");
            Check(sc.Pictures.Chosen == null, "bỏ chọn");
            sc.Undo(); Eq(1, sc.Current.Marks.Count, "hoàn tác xoá");
            sc.Undo(); Near(0, im.Angle, 0.01, "hoàn tác xoay");
        });

        Console.WriteLine("\nTrang, hoàn tác, chữ");
        Run("hoàn tác / làm lại", () =>
        {
            var p = new Sheet();
            var a = Text("a", 0, 0); var b = Text("b", 0, 60);
            p.Add(a); p.Add(b);
            p.Undo(); Eq(1, p.Marks.Count, "sau hoàn tác"); Check(p.CanRedo, "làm lại được");
            p.Redo(); Eq(2, p.Marks.Count, "sau làm lại");
            p.Remove(new List<Mark> { a, b }); Eq(0, p.Marks.Count, "xoá hết");
            p.Undo(); Eq(2, p.Marks.Count, "hoàn tác xoá hết");
        });
        Run("trang mới chỉ thêm khi trang đang viết có nội dung; quay lại bỏ trang trống cuối", () =>
        {
            var sc = new Stage(); sc.Resize(1280, 720); sc.RefH = 720;
            sc.SetBoard("green");
            sc.NextPage(); Eq("1 / 1", sc.Book.PageLabel, "trang trống");
            sc.AddStamp("sao", new PointF(100, 100));
            sc.NextPage(); Eq("2 / 2", sc.Book.PageLabel, "thêm trang");
            sc.PrevPage(); Eq("1 / 1", sc.Book.PageLabel, "bỏ trang trống cuối");
        });
        Run("mở bài: bảng theo trang đầu, đổi trang thì đổi bảng", () =>
        {
            var sc = new Stage(); sc.Resize(1280, 720); sc.RefH = 720;
            sc.SetBoard("green");
            sc.LoadPages(Sample(), "black");
            Eq("green", sc.Board, "trang 1");
            sc.NextPage(); Eq("oli", sc.Board, "trang 2");
            Check(sc.Book.HasLesson, "có nội dung");
        });
        Run("chữ có ký hiệu toán được vẽ (phông dự phòng), không bỏ trống", () =>
        {
            float w1, w2;
            using (var p1 = TextRuns.Build("√16 ≥ 0 ∠ABC", "chalk", 48, PointF.Empty, out w1))
            using (var p2 = TextRuns.Build("√16 ≥ 0 ∠ABC △", "chalk", 48, PointF.Empty, out w2))
            {
                Check(p1.PointCount > 50, "có nét chữ");
                Check(w2 > w1 + 10, "thêm ký hiệu thì dài thêm");
            }
            Check(Fonts.Fallbacks.Length > 0, "có phông ký hiệu của Windows");
        });
        Run("phím tắt: đọc đúng, giá trị hỏng thì dùng mặc định", () =>
        {
            var def = new KeyCombo(3, System.Windows.Forms.Keys.P);
            Eq("2,66", KeyCombo.FromStored("2,66", def).Stored, "hợp lệ");
            Eq(def.Stored, KeyCombo.FromStored("abc", def).Stored, "chữ");
            Eq(def.Stored, KeyCombo.FromStored(null, def).Stored, "null");
            Eq("Ctrl + Alt + P", def.Display, "hiển thị");
            Eq("Không có", new KeyCombo(0, System.Windows.Forms.Keys.None).Display, "không có phím tắt");
        });
        Run("lịch sử hoàn tác chỉ giữ " + Sheet.HistoryLimit + " bước gần nhất", () =>
        {
            var p = new Sheet();
            for (int i = 0; i < Sheet.HistoryLimit + 100; i++) p.Add(Stroke(2, i, 0));
            int undone = 0;
            while (p.Undo()) undone++;
            Eq(Sheet.HistoryLimit, undone, "số bước hoàn tác");
            Eq(100, p.Marks.Count, "nét cũ hơn vẫn còn trên trang");
        });
        Run("khăn lau: cả lần lau là một bước hoàn tác", () =>
        {
            var sc = new Stage(); sc.Resize(1280, 720); sc.RefH = 720; sc.SetBoard("white");
            sc.AddStamp("sao", new PointF(100, 100));
            sc.AddStamp("sao", new PointF(300, 100));
            sc.EraseAt(new PointF(100, 100));
            sc.EraseAt(new PointF(300, 100));
            sc.EndAll();
            Eq(0, sc.Current.Marks.Count, "đã lau");
            sc.Undo();
            Eq(2, sc.Current.Marks.Count, "một lần hoàn tác lấy lại cả hai");
        });
        Run("ảnh mở từ bài giảng giữ nguyên dữ liệu (không nén lại mỗi lần lưu)", () =>
        {
            var jpg = Jpg(300, 200);
            Check(PictureMark.FromBytes(jpg, true).Data.SequenceEqual(jpg), "giữ nguyên byte");
            var big = PictureMark.FromBytes(Png(3200, 100), true);
            Eq(1600, big.Img.Width, "ảnh quá cỡ vẫn được thu nhỏ");
        });
        Run("xuất bài giảng thành PDF: mỗi trang một trang PDF, mở lại được bằng trình đọc", () =>
        {
            string f = Path.Combine(Tmp, "bài giảng.pdf");
            int n = Lesson.ExportPdf(f, Sample(), 1280, 720, "green", "Bài 5: Phân số");
            Eq(2, n, "số trang");
            var bytes = File.ReadAllBytes(f);
            string text = Encoding.GetEncoding(28591).GetString(bytes);
            Check(text.StartsWith("%PDF-1.4"), "đầu tệp PDF");
            Check(text.TrimEnd().EndsWith("%%EOF"), "cuối tệp PDF");
            Check(text.Contains("/Count 2"), "cây trang có 2 trang");
            Eq(2, System.Text.RegularExpressions.Regex.Matches(text, "/Subtype /Image").Count, "2 ảnh trang");
            Check(text.Contains("/Width 1920 /Height 1080"), "trang vẽ ở 1080p cho rõ nét khi in");
            // the cross-reference table must point at each object exactly
            int xref = int.Parse(System.Text.RegularExpressions.Regex.Match(text, @"startxref\n(\d+)").Groups[1].Value);
            Check(text.Substring(xref).StartsWith("xref"), "startxref trỏ đúng bảng xref");
            var entries = System.Text.RegularExpressions.Regex.Matches(text.Substring(xref), @"(\d{10}) 00000 n");
            for (int i = 0; i < entries.Count; i++)
                Check(text.Substring(int.Parse(entries[i].Groups[1].Value)).StartsWith((i + 1) + " 0 obj"), "vị trí đối tượng " + (i + 1));
            Check(!File.Exists(f + ".tmp"), "không để lại tệp tạm");
        });
        Run("xuất PowerPoint mới: mỗi trang một trang chiếu ảnh, XML hợp lệ", () =>
        {
            string f = Path.Combine(Tmp, "bài giảng.pptx");
            Eq(2, Lesson.ExportPptx(f, Sample(), 1280, 720, "green", false, "Bài 5"), "số trang chiếu");
            using (var zip = System.IO.Compression.ZipFile.OpenRead(f))
            {
                var names = zip.Entries.Select(e => e.FullName).ToList();
                foreach (var part in new[] { "[Content_Types].xml", "_rels/.rels", "ppt/presentation.xml", "ppt/slides/slide1.xml", "ppt/slides/slide2.xml",
                    "ppt/slides/_rels/slide2.xml.rels", "ppt/media/image2.jpeg", "ppt/slideMasters/slideMaster1.xml", "ppt/slideLayouts/slideLayout1.xml", "ppt/theme/theme1.xml" })
                    Check(names.Contains(part), "có " + part);
                foreach (var e in zip.Entries.Where(e => e.FullName.EndsWith(".xml") || e.FullName.EndsWith(".rels")))
                    using (var s = e.Open()) System.Xml.Linq.XDocument.Load(s);   // every part parses
                var pres = Part(zip, "ppt/presentation.xml");
                Eq(2, System.Text.RegularExpressions.Regex.Matches(pres, "<p:sldId ").Count, "2 trang chiếu trong danh sách");
                Check(pres.Contains("cx=\"12192000\" cy=\"6858000\""), "khổ 16:9 như màn hình");
            }
            Check(!File.Exists(f + ".tmp"), "không để lại tệp tạm");
        });
        Run("thêm vào PowerPoint có sẵn: giữ trang cũ, thêm vào cuối, lặp lại được", () =>
        {
            string f = Path.Combine(Tmp, "có sẵn.pptx");
            Lesson.ExportPptx(f, Sample(), 1280, 720, "green", false, "Bài cũ");
            Eq(2, Lesson.ExportPptx(f, Sample(), 1024, 768, "white", true, null), "thêm 2 trang lần 1");
            Eq(2, Lesson.ExportPptx(f, Sample(), 1024, 768, "white", true, null), "thêm 2 trang lần 2");
            using (var zip = System.IO.Compression.ZipFile.OpenRead(f))
            {
                var pres = Part(zip, "ppt/presentation.xml");
                var ids = System.Text.RegularExpressions.Regex.Matches(pres, "<p:sldId id=\"(\\d+)\" r:id=\"([^\"]+)\"").Cast<System.Text.RegularExpressions.Match>().ToList();
                Eq(6, ids.Count, "6 trang chiếu");
                Eq(6, ids.Select(m => m.Groups[1].Value).Distinct().Count(), "id trang chiếu không trùng");
                var rels = Part(zip, "ppt/_rels/presentation.xml.rels");
                foreach (var m in ids) Check(rels.Contains("Id=\"" + m.Groups[2].Value + "\""), "quan hệ " + m.Groups[2].Value + " có thật");
                Check(zip.GetEntry("ppt/slides/slide6.xml") != null, "slide6.xml");
                Eq(6, zip.Entries.Count(e => e.FullName.StartsWith("ppt/media/")), "6 ảnh, không ảnh nào bị ghi đè");
                Eq(6, System.Text.RegularExpressions.Regex.Matches(Part(zip, "[Content_Types].xml"), "presentationml.slide\\+xml").Count, "kiểu nội dung cho 6 trang");
                // a 4:3 page in a 16:9 deck is fitted inside the slide, centred
                var s3 = Part(zip, "ppt/slides/slide3.xml");
                Check(s3.Contains("<a:off x=\"1524000\" y=\"0\"/><a:ext cx=\"9144000\" cy=\"6858000\"/>"), "ảnh 4:3 nằm giữa trang 16:9");
            }
        });
        Run("thêm vào tệp không phải PowerPoint → báo lỗi rõ ràng, tệp gốc còn nguyên", () =>
        {
            string f = Path.Combine(Tmp, "giả.pptx");
            File.WriteAllText(f, "không phải pptx");
            Throws<InvalidDataException>(() => Lesson.ExportPptx(f, Sample(), 1280, 720, "green", true, null), "tệp chữ");
            Eq("không phải pptx", File.ReadAllText(f), "tệp gốc");
            Check(!File.Exists(f + ".tmp"), "không để lại tệp tạm");
        });
        Run("dòng kẻ khi gõ: bấm đâu cũng rơi đúng đầu một dòng", () =>
        {
            var sc = new Stage();
            sc.Resize(1920, 1080);
            float px = 54, step = px * 1.3f, top = 1080 * 0.08f;
            Near(top, sc.SnapToLine(top + 5, px), 0.01, "dòng đầu");
            Near(top + 3 * step, sc.SnapToLine(top + 3 * step + step * 0.9f, px), 0.01, "dòng 4");
            Check(sc.SnapToLine(3, px) >= 0, "không lên khỏi mép trên");
        });
        Run("giữ chuột rồi buông ở bất cứ đâu (kể cả trên thanh công cụ): luôn báo đã buông", () =>
        {
            var seen = new List<GateEvent>();
            var gate = new ClickGate { Capturing = true, Holes = new[] { new RECT(0, 900, 1920, 1080) }, BypassProbe = () => false, Report = e => seen.Add(e) };
            Check(gate.Decide(Win32.Wm.LButtonDown, false, new POINT(500, 400)), "giữ nút trên bảng: không lọt xuống slide");
            Check(gate.Decide(Win32.Wm.LButtonUp, false, new POINT(600, 1000)), "buông trên thanh công cụ: vẫn là của Phấn Trắng");
            Eq(2, seen.Count, "số sự kiện");
            Eq(GateKind.Down, seen[0].Kind, "nhấn");
            Eq(GateKind.Up, seen[1].Kind, "buông");
        });
        Run("nhấn trên thanh công cụ rồi buông ngoài thanh: đi thẳng xuống Windows, không thành nét vẽ", () =>
        {
            var seen = new List<GateEvent>();
            var gate = new ClickGate { Capturing = true, Holes = new[] { new RECT(0, 900, 1920, 1080) }, BypassProbe = () => false, Report = e => seen.Add(e) };
            Check(!gate.Decide(Win32.Wm.LButtonDown, false, new POINT(600, 1000)), "nhấn trên thanh");
            Check(!gate.Decide(Win32.Wm.LButtonUp, false, new POINT(600, 300)), "buông ngoài thanh");
            Eq(0, seen.Count(e => e.Kind == GateKind.Down || e.Kind == GateKind.Up), "không có nhấn/buông nào cho bảng");
        });
        Run("giữ phím Alt: nhấn và buông đều đi xuống slide, báo đủ cặp", () =>
        {
            var seen = new List<GateEvent>();
            bool alt = true;
            var gate = new ClickGate { Capturing = true, BypassProbe = () => alt, Report = e => seen.Add(e) };
            Check(!gate.Decide(Win32.Wm.LButtonDown, false, new POINT(10, 10)), "nhấn");
            alt = false;   // Alt let go before the button: the release still belongs to the slide
            Check(!gate.Decide(Win32.Wm.LButtonUp, false, new POINT(10, 10)), "buông");
            Eq(GateKind.BypassUp, seen.Last().Kind, "báo buông");
        });
        Run("bộ bắt chuột dừng giữa lúc đang giữ (Windows gỡ hook, tắt app): tự báo đã buông", () =>
        {
            var seen = new List<GateEvent>();
            var gate = new ClickGate { Capturing = true, BypassProbe = () => false, Report = e => seen.Add(e) };
            gate.Decide(Win32.Wm.LButtonDown, false, new POINT(500, 400));
            gate.Decide(Win32.Wm.RButtonDown, false, new POINT(500, 400));
            gate.Stop();
            Eq(2, seen.Count(e => e.Kind == GateKind.Up), "báo buông cả hai nút");
            Check(!gate.Decide(Win32.Wm.LButtonUp, false, new POINT(500, 400)), "lần buông thật đến muộn không bị nuốt nữa");
        });
        Run("nhấn giả lập (phần mềm khác gửi) không bị giữ lại", () =>
        {
            var seen = new List<GateEvent>();
            var gate = new ClickGate { Capturing = true, BypassProbe = () => false, Report = e => seen.Add(e) };
            Check(!gate.Decide(Win32.Wm.LButtonDown, true, new POINT(500, 400)), "nhấn giả lập");
            Check(!gate.Decide(Win32.Wm.LButtonUp, true, new POINT(500, 400)), "buông giả lập");
            Eq(0, seen.Count, "không báo gì");
        });
        Run("lưới an toàn: Windows làm mất tin buông chuột → tự kết thúc sau chốc lát", () =>
        {
            var g = new HoldGuard();
            g.RawDown(0, 10.00); g.Pressed(0, 10.01);
            Check(!g.Stuck(0, 11), "còn giữ thật: không đụng tới");
            g.RawUp(0, 12.0);
            Check(!g.Stuck(0, 12.1), "cho bộ bắt chuột kịp báo trước");
            Check(g.Stuck(0, 12.0 + HoldGuard.Grace), "quá hạn: coi như đã buông");
            g.Released(0);
            Check(!g.Stuck(0, 20), "đã buông thì thôi");
        });
        Run("lưới an toàn: bấm rất nhanh (tin buông thô đến trước tin nhấn) vẫn đúng", () =>
        {
            var g = new HoldGuard();
            g.RawDown(0, 5.00); g.RawUp(0, 5.02); g.Pressed(0, 5.03);
            Check(!g.Stuck(0, 5.1), "chờ bộ bắt chuột");
            Check(g.Stuck(0, 5.4), "rồi mới tự buông");
        });
        Run("lưới an toàn: bút cảm ứng/màn chạm (không có tin thô) không bị cắt nét", () =>
        {
            var g = new HoldGuard();
            g.RawDown(0, 1.0); g.RawUp(0, 1.1);   // an old mouse click
            g.Pressed(0, 30.0);                  // a pen press: raw input says nothing
            Check(!g.Stuck(0, 60), "nét bút dài vẫn giữ");
            g.RawDown(1, 40); g.RawUp(1, 40.1);
            Check(!g.Stuck(0, 60), "nút phải không ảnh hưởng nút trái");
        });
        Console.WriteLine("\nLớp học: danh sách, gọi tên, chia nhóm, thi đua, đếm ngược");
        Run("nhập từng dòng: bỏ dòng trống, số thứ tự, khoảng trắng thừa", () =>
        {
            var n = ClassRoster.Clean(new[] { "Danh sách lớp 6A", "1. Nguyễn Văn An", "","  2)  Trần   Thị Bình ", "3 - Lê Chi", "15", "\tPhạm Dũng" });
            Eq("Nguyễn Văn An|Trần Thị Bình|Lê Chi|Phạm Dũng", string.Join("|", n), "danh sách");
        });
        Run("đọc tệp .txt (UTF-8 và bảng mã Windows cũ) và .csv có cột Họ và tên", () =>
        {
            string txt = Path.Combine(Tmp, "lop.txt");
            File.WriteAllText(txt, "An\r\nBình\r\n\r\nChi\r\n", new UTF8Encoding(true));
            Eq(3, ClassRoster.FromFile(txt).Count, "txt UTF-8");
            string csv = Path.Combine(Tmp, "lop.csv");
            File.WriteAllText(csv, "STT,Họ và tên,Ngày sinh\r\n1,\"Nguyễn Văn An\",01/02/2014\r\n2,Trần Thị Bình,03/04/2014\r\n", new UTF8Encoding(false));
            Eq("Nguyễn Văn An|Trần Thị Bình", string.Join("|", ClassRoster.FromFile(csv)), "csv");
        });
        Run("đọc tệp Excel .xlsx: cột Họ đệm + Tên ghép lại, bỏ STT và ngày sinh", () =>
        {
            string f = Path.Combine(Tmp, "lop.xlsx");
            WriteXlsx(f, new[] { "Danh sách lớp 6A", "STT", "Họ đệm", "Tên", "Ngày sinh", "Nguyễn Văn", "An", "Trần Thị", "Bình" },
                "<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c></row>" +
                "<row r=\"3\"><c r=\"A3\" t=\"s\"><v>1</v></c><c r=\"B3\" t=\"s\"><v>2</v></c><c r=\"C3\" t=\"s\"><v>3</v></c><c r=\"D3\" t=\"s\"><v>4</v></c></row>" +
                "<row r=\"4\"><c r=\"A4\"><v>1</v></c><c r=\"B4\" t=\"s\"><v>5</v></c><c r=\"C4\" t=\"s\"><v>6</v></c><c r=\"D4\"><v>41640</v></c></row>" +
                "<row r=\"5\"><c r=\"A5\"><v>2</v></c><c r=\"B5\" t=\"s\"><v>7</v></c><c r=\"C5\" t=\"inlineStr\"><is><t>Bình</t></is></c></row>");
            Eq("Nguyễn Văn An|Trần Thị Bình", string.Join("|", ClassRoster.FromFile(f)), "xlsx");
        });
        Run("Excel không có tiêu đề: lấy cột nhiều chữ nhất; tệp hỏng/.xls báo lỗi rõ ràng", () =>
        {
            var rows = new List<string[]> { new[] { "1", "Hoa", "8.5" }, new[] { "2", "Minh", "9" }, new[] { "3", "Lan", "7" } };
            Eq("Hoa|Minh|Lan", string.Join("|", ClassRoster.FromRows(rows)), "không tiêu đề");
            string bad = Path.Combine(Tmp, "hong.xlsx");
            File.WriteAllText(bad, "không phải excel");
            Throws<InvalidDataException>(() => ClassRoster.FromFile(bad), "xlsx hỏng");
            string xls = Path.Combine(Tmp, "cu.xls");
            File.WriteAllText(xls, "x");
            Throws<InvalidDataException>(() => ClassRoster.FromFile(xls), ".xls");
        });
        Run("gọi tên: ai cũng được gọi một lần trước khi gọi lại", () =>
        {
            var p = new NamePicker(new[] { "An", "Bình", "Chi", "Dũng", "Em" }, new Random(3));
            var first = Enumerable.Range(0, 5).Select(i => p.Next()).ToList();
            Eq(5, first.Distinct().Count(), "vòng 1 không trùng");
            Eq(0, p.Left, "hết lượt");
            Check(p.Next() != null, "tự sang vòng mới");
            Eq(4, p.Left, "vòng 2 còn 4");
            Check(new NamePicker(new string[0]).Next() == null, "danh sách rỗng");
        });
        Run("chia nhóm: đủ người, các nhóm chênh nhau tối đa 1", () =>
        {
            var names = Enumerable.Range(1, 23).Select(i => "HS" + i).ToList();
            var g = Grouping.IntoGroups(names, 4, new Random(1));
            Eq(4, g.Count, "4 nhóm");
            Eq(23, g.Sum(x => x.Count), "đủ 23 người");
            Check(g.Max(x => x.Count) - g.Min(x => x.Count) <= 1, "đều nhau");
            Eq(23, g.SelectMany(x => x).Distinct().Count(), "không ai bị lặp");
            var s = Grouping.BySize(names, 5, new Random(2));
            Eq(5, s.Count, "nhóm 5 người → 5 nhóm (4-5 người)");
            Eq(3, Grouping.IntoGroups(names.Take(3).ToList(), 10, new Random(1)).Count, "không có nhóm rỗng");
        });
        Run("thi đua: cộng trừ điểm, hoàn tác, đội dẫn đầu (hoà thì cùng dẫn)", () =>
        {
            var c = Contest.Numbered(3, "Tổ");
            Eq("Tổ 1|Tổ 2|Tổ 3", string.Join("|", c.Teams), "tên đội");
            Eq(0, c.Leaders().Count, "chưa ai có điểm");
            c.Add(0, 1); c.Add(1, 2); c.Add(0, 1);
            Eq("0,1", string.Join(",", c.Leaders()), "hoà 2 điểm");
            c.Add(2, -1);
            Eq(-1, c.Scores[2], "điểm âm");
            Eq(2, c.Undo(), "hoàn tác bước cuối");
            Eq(0, c.Scores[2], "trả lại điểm");
            c.ResetScores();
            Check(!c.CanUndo && c.Scores.All(x => x == 0), "làm lại từ đầu");
        });
        Run("đếm ngược: tạm dừng, chạy tiếp, thêm giờ, hết giờ", () =>
        {
            var t = new Countdown(60);
            t.Start(100);
            Near(50, t.Remaining(110), 1e-9, "sau 10 giây");
            t.Pause(110);
            Near(50, t.Remaining(500), 1e-9, "dừng thì không trôi");
            t.Start(500);
            t.AddTime(30, 505);
            Near(75, t.Remaining(505), 1e-9, "thêm 30 giây");
            Check(t.Ended(600), "hết giờ");
            Eq("01:15", Countdown.Format(74.2), "hiển thị làm tròn lên");
            Eq("00:00", Countdown.Format(-3), "không âm");
            t.Reset();
            Check(!t.Running, "đặt lại");
        });
        Run("vẽ sát mép thước kẻ: nét chạy thẳng theo mép, kể cả khi thước xoay", () =>
        {
            var ruler = new GeoTool("ruler", new PointF(100, 100), 50);
            var p = new PointF(500, 140);
            Check(ruler.Snap(new PointF(300, 90), ref p, 26), "bắt đầu cách mép 10px");
            Near(90, p.Y, 0.01, "đi song song mép trên");
            Near(500, p.X, 0.01, "giữ vị trí dọc theo mép");
            var far = new PointF(500, 140);
            Check(!ruler.Snap(new PointF(300, 30), ref far, 26), "xa mép: vẽ tự do");
            ruler.Angle = 30;
            var q = new PointF(400, 400);
            var start = new PointF(100 + 200 * (float)Math.Cos(Math.PI / 6), 100 + 200 * (float)Math.Sin(Math.PI / 6));
            Check(ruler.Snap(start, ref q, 26), "thước xoay 30°");
            Near(30, Math.Atan2(q.Y - start.Y, q.X - start.X) * 180 / Math.PI, 0.01, "nét theo góc 30°");
            ruler.Dispose();
        });
        Run("ê-ke: vẽ dọc cạnh góc vuông và cạnh huyền", () =>
        {
            var sq = new GeoTool("set45", new PointF(0, 0), 50);   // legs along +x and up (-y), 500 px
            var p = new PointF(-5, -300);
            Check(sq.Snap(new PointF(-8, -100), ref p, 26), "cạnh đứng");
            Near(-8, p.X, 0.01, "thẳng đứng");
            var h = new PointF(400, -400);
            Check(sq.Snap(new PointF(255, -255), ref h, 26), "cạnh huyền");
            Near(0, h.X + h.Y, 0.01, "chéo 45°");
            sq.Dispose();
        });
        Console.WriteLine("\nSố liệu sử dụng ẩn danh");
        Run("gói tin chỉ có mã máy, phiên bản, kênh, Windows và số đếm", () =>
        {
            var n = new Dictionary<string, int> { { "tool.draw", 5 }, { "start", 1 }, { "export.pptx", 2 } };
            string json = Usage.Payload("0123456789abcdef0123456789abcdef", "1.3.0", "tai-ve", "10.0.26200.0", n);
            Eq("{\"id\":\"0123456789abcdef0123456789abcdef\",\"v\":\"1.3.0\",\"ch\":\"tai-ve\",\"os\":\"10.0.26200.0\",\"n\":{\"export.pptx\":2,\"start\":1,\"tool.draw\":5}}", json, "json");
            var parsed = new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            Eq(5, parsed.Count, "đúng 5 trường");
        });
        Run("gói tin không mang được chữ tự do (tên, đường dẫn, dấu ngoặc kép)", () =>
        {
            var n = new Dictionary<string, int> { { "Nguyễn Văn An", 1 }, { "C:\\Users\\cô giáo\\bai.pptx", 1 }, { "x\",\"y", 1 }, { "tool.type", 0 }, { "class.picker", 3 } };
            string json = Usage.Payload("ab\"cd", "1.3\"}", "tai-ve", "10.0", n);
            Eq("{\"id\":\"abcd\",\"v\":\"1.3\",\"ch\":\"tai-ve\",\"os\":\"10.0\",\"n\":{\"class.picker\":3}}", json, "lọc sạch");
            Check(new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(json) != null, "vẫn là JSON hợp lệ");
        });
        Run("gói tin giới hạn số mục và số đếm", () =>
        {
            var n = Enumerable.Range(0, 100).ToDictionary(i => "k" + i, i => 999999999);
            string json = Usage.Payload("id", "1", "store", "10", n);
            Eq(Usage.MaxKeys, System.Text.RegularExpressions.Regex.Matches(json, "\"k\\d+\"").Count, "tối đa 60 mục");
            Check(!json.Contains("999999999") && json.Contains(":" + Usage.MaxCount), "số đếm bị chặn trên");
        });
        Run("đường thẳng giữ Shift: nấc 45°, hình vuông/tròn đều", () =>
        {
            var p = ShapeMark.Constrain("line", new PointF(0, 0), new PointF(100, 90));
            Near(p.X, p.Y, 0.01, "45°");
            var q = ShapeMark.Constrain("rect", new PointF(0, 0), new PointF(-40, 100));
            Near(-100, q.X, 0.01, "x"); Near(100, q.Y, 0.01, "y");
        });

        try { Directory.Delete(Tmp, true); } catch { }
        Console.WriteLine("\n" + pass + " đạt, " + fail + " lỗi");
        foreach (var f in failures) Console.WriteLine("  - " + f);
        return fail == 0 ? 0 : 1;
    }
}
