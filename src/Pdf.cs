using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PhanTrang
{
    /// <summary>One PDF page: a JPEG picture filling the whole page.</summary>
    public sealed class PdfPage
    {
        public byte[] Jpeg;
        public int PixelWidth, PixelHeight;
    }

    /// <summary>
    /// Writes a small PDF 1.4 file in which every page is one JPEG picture (embedded as is, DCTDecode).
    /// That is all a board page needs, so no PDF library has to ship with the app.
    /// </summary>
    public static class PdfWriter
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>A text string for the document info: UTF-16 with a byte order mark, as hex, so Vietnamese survives.</summary>
        static string PdfText(string s)
        {
            var sb = new StringBuilder("<FEFF");
            foreach (char c in s ?? "") sb.Append(((int)c).ToString("X4", Inv));
            return sb.Append('>').ToString();
        }

        /// <summary>Writes the pages to 'path' (through a temporary file). Pages are widthPt points wide, height to scale.</summary>
        public static void Write(string path, IList<PdfPage> pages, double widthPt, string title)
        {
            if (pages == null || pages.Count == 0) throw new ArgumentException("Không có trang nào để xuất.");
            string full = Path.GetFullPath(path), tmp = full + ".tmp";
            using (var file = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                WriteTo(file, pages, widthPt, title);
            if (File.Exists(full)) File.Delete(full);
            File.Move(tmp, full);
        }

        public static void WriteTo(Stream output, IList<PdfPage> pages, double widthPt, string title)
        {
            // object numbers: 1 catalog, 2 page tree, 3 info, then per page: page, contents, image
            int count = 3 + pages.Count * 3;
            var offsets = new long[count + 1];
            var latin = Encoding.GetEncoding(28591);   // PDF syntax is bytes; everything written as text here is ASCII
            Action<string> put = s => { var b = latin.GetBytes(s); output.Write(b, 0, b.Length); };
            Func<int, int> pageObj = i => 4 + i * 3;

            put("%PDF-1.4\n%âãÏÓ\n");   // the binary comment tells tools the file holds binary data

            offsets[1] = output.Position;
            put("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

            offsets[2] = output.Position;
            var kids = new StringBuilder();
            for (int i = 0; i < pages.Count; i++) kids.Append(pageObj(i)).Append(" 0 R ");
            put("2 0 obj\n<< /Type /Pages /Kids [ " + kids + "] /Count " + pages.Count + " >>\nendobj\n");

            offsets[3] = output.Position;
            put("3 0 obj\n<< /Title " + PdfText(title) + " /Producer " + PdfText("Phấn Trắng") +
                " /CreationDate (D:" + DateTime.Now.ToString("yyyyMMddHHmmss", Inv) + ") >>\nendobj\n");

            for (int i = 0; i < pages.Count; i++)
            {
                var p = pages[i];
                double w = widthPt, h = widthPt * p.PixelHeight / p.PixelWidth;
                string W = w.ToString("0.##", Inv), H = h.ToString("0.##", Inv);
                int page = pageObj(i), contents = page + 1, image = page + 2;

                offsets[page] = output.Position;
                put(page + " 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + W + " " + H + "] " +
                    "/Resources << /XObject << /Im0 " + image + " 0 R >> >> /Contents " + contents + " 0 R >>\nendobj\n");

                string draw = "q " + W + " 0 0 " + H + " 0 0 cm /Im0 Do Q\n";   // stretch the 1x1 image space over the page
                offsets[contents] = output.Position;
                put(contents + " 0 obj\n<< /Length " + draw.Length + " >>\nstream\n" + draw + "endstream\nendobj\n");

                offsets[image] = output.Position;
                put(image + " 0 obj\n<< /Type /XObject /Subtype /Image /Width " + p.PixelWidth + " /Height " + p.PixelHeight +
                    " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length " + p.Jpeg.Length + " >>\nstream\n");
                output.Write(p.Jpeg, 0, p.Jpeg.Length);
                put("\nendstream\nendobj\n");
            }

            long xref = output.Position;
            var table = new StringBuilder("xref\n0 " + (count + 1) + "\n0000000000 65535 f \n");
            for (int i = 1; i <= count; i++) table.Append(offsets[i].ToString("D10", Inv)).Append(" 00000 n \n");
            put(table.ToString());
            put("trailer\n<< /Size " + (count + 1) + " /Root 1 0 R /Info 3 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");
        }
    }
}
