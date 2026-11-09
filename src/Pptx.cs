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
    /// Writes board pages into PowerPoint (.pptx), one picture per slide: either a new presentation, or added at
    /// the end of one the teacher already has. A .pptx is a zip of XML parts, so nothing has to ship with the app.
    /// The pictures are the same JPEGs as in the PDF export.
    /// </summary>
    public static class PptxWriter
    {
        const string NsP = "http://schemas.openxmlformats.org/presentationml/2006/main";
        const string NsA = "http://schemas.openxmlformats.org/drawingml/2006/main";
        const string NsR = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string NsRel = "http://schemas.openxmlformats.org/package/2006/relationships";
        const string NsCt = "http://schemas.openxmlformats.org/package/2006/content-types";
        const string RelBase = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
        const string CtSlide = "application/vnd.openxmlformats-officedocument.presentationml.slide+xml";
        const long WideCx = 12192000;   // 13.333 inch: PowerPoint's own widescreen width, in EMU

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        // ---------------- a new presentation ----------------

        /// <summary>A new .pptx with one slide per page; the slide has the pages' proportions.</summary>
        public static void WriteNew(string path, IList<PdfPage> pages, string title)
        {
            if (pages == null || pages.Count == 0) throw new ArgumentException("Không có trang nào để xuất.");
            long cx = WideCx, cy = (long)Math.Round(WideCx * (double)pages[0].PixelHeight / pages[0].PixelWidth);
            cy = Math.Max(914400, Math.Min(51206400, cy));
            string full = Path.GetFullPath(path), tmp = full + ".tmp";
            try
            {
                using (var file = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
                {
                    var types = new StringBuilder();
                    types.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<Types xmlns=\"" + NsCt + "\">");
                    types.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
                    types.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
                    types.Append("<Default Extension=\"jpeg\" ContentType=\"image/jpeg\"/>");
                    types.Append("<Override PartName=\"/ppt/presentation.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml\"/>");
                    types.Append("<Override PartName=\"/ppt/slideMasters/slideMaster1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slideMaster+xml\"/>");
                    types.Append("<Override PartName=\"/ppt/slideLayouts/slideLayout1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml\"/>");
                    types.Append("<Override PartName=\"/ppt/theme/theme1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.theme+xml\"/>");
                    types.Append("<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>");
                    for (int i = 1; i <= pages.Count; i++) types.Append("<Override PartName=\"/ppt/slides/slide" + i + ".xml\" ContentType=\"" + CtSlide + "\"/>");
                    types.Append("</Types>");
                    Put(zip, "[Content_Types].xml", types.ToString());

                    Put(zip, "_rels/.rels", Rels(
                        Rel("rId1", "officeDocument", "ppt/presentation.xml"),
                        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>"));
                    Put(zip, "docProps/core.xml", Core(title));

                    var pres = new StringBuilder();
                    pres.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<p:presentation xmlns:a=\"" + NsA + "\" xmlns:r=\"" + NsR + "\" xmlns:p=\"" + NsP + "\">");
                    pres.Append("<p:sldMasterIdLst><p:sldMasterId id=\"2147483648\" r:id=\"rId1\"/></p:sldMasterIdLst><p:sldIdLst>");
                    for (int i = 1; i <= pages.Count; i++) pres.Append("<p:sldId id=\"" + (255 + i) + "\" r:id=\"rId" + (i + 2) + "\"/>");
                    pres.Append("</p:sldIdLst><p:sldSz cx=\"" + cx + "\" cy=\"" + cy + "\"/><p:notesSz cx=\"6858000\" cy=\"9144000\"/></p:presentation>");
                    Put(zip, "ppt/presentation.xml", pres.ToString());

                    var presRels = new List<string> { Rel("rId1", "slideMaster", "slideMasters/slideMaster1.xml"), Rel("rId2", "theme", "theme/theme1.xml") };
                    for (int i = 1; i <= pages.Count; i++) presRels.Add(Rel("rId" + (i + 2), "slide", "slides/slide" + i + ".xml"));
                    Put(zip, "ppt/_rels/presentation.xml.rels", Rels(presRels.ToArray()));

                    Put(zip, "ppt/slideMasters/slideMaster1.xml", Master);
                    Put(zip, "ppt/slideMasters/_rels/slideMaster1.xml.rels", Rels(Rel("rId1", "slideLayout", "../slideLayouts/slideLayout1.xml"), Rel("rId2", "theme", "../theme/theme1.xml")));
                    Put(zip, "ppt/slideLayouts/slideLayout1.xml", Layout);
                    Put(zip, "ppt/slideLayouts/_rels/slideLayout1.xml.rels", Rels(Rel("rId1", "slideMaster", "../slideMasters/slideMaster1.xml")));
                    Put(zip, "ppt/theme/theme1.xml", Theme1);

                    for (int i = 0; i < pages.Count; i++)
                    {
                        string media = "image" + (i + 1) + ".jpeg";
                        PutBytes(zip, "ppt/media/" + media, pages[i].Jpeg);
                        Put(zip, "ppt/slides/slide" + (i + 1) + ".xml", Slide(pages[i], cx, cy, i + 1));
                        Put(zip, "ppt/slides/_rels/slide" + (i + 1) + ".xml.rels", Rels(Rel("rId1", "slideLayout", "../slideLayouts/slideLayout1.xml"), Rel("rId2", "image", "../media/" + media)));
                    }
                }
            }
            catch
            {
                try { File.Delete(tmp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                throw;
            }
            Swap(tmp, full);
        }

        // ---------------- added to an existing presentation ----------------

        /// <summary>
        /// Adds one slide per page at the end of an existing .pptx (the teacher's own slides stay as they are).
        /// The picture is fitted inside the slide, keeping its proportions. Returns the number of slides added.
        /// Problems with the file become an InvalidDataException with a message for the teacher.
        /// </summary>
        public static int Append(string path, IList<PdfPage> pages)
        {
            if (pages == null || pages.Count == 0) throw new ArgumentException("Không có trang nào để xuất.");
            string full = Path.GetFullPath(path), tmp = full + ".tmp";
            File.Copy(full, tmp, true);
            try
            {
                using (var file = new FileStream(tmp, FileMode.Open, FileAccess.ReadWrite))
                {
                    ZipArchive zip;
                    try { zip = new ZipArchive(file, ZipArchiveMode.Update); }
                    catch (InvalidDataException) { throw new InvalidDataException("Tệp này không phải bài PowerPoint (.pptx), hoặc đang được đặt mật khẩu."); }
                    using (zip) AppendInto(zip, pages);
                }
                Swap(tmp, full);
            }
            catch
            {
                try { File.Delete(tmp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                throw;
            }
            return pages.Count;
        }

        static void AppendInto(ZipArchive zip, IList<PdfPage> pages)
        {
            XNamespace p = NsP, r = NsR, rel = NsRel, ct = NsCt;

            // where the presentation part is (almost always ppt/presentation.xml)
            var rootRels = Load(zip, "_rels/.rels");
            var office = rootRels == null ? null : rootRels.Root.Elements(rel + "Relationship").FirstOrDefault(e => ((string)e.Attribute("Type") ?? "").EndsWith("/officeDocument"));
            if (office == null) throw new InvalidDataException("Tệp này không phải bài PowerPoint (.pptx).");
            string presPath = ((string)office.Attribute("Target") ?? "").TrimStart('/');
            string presDir = presPath.Contains('/') ? presPath.Substring(0, presPath.LastIndexOf('/')) : "";
            string prefix = presDir.Length > 0 ? presDir + "/" : "";
            string presRelsPath = prefix + "_rels/" + presPath.Substring(prefix.Length) + ".rels";

            var pres = Load(zip, presPath);
            if (pres == null || pres.Root.Name.LocalName != "presentation") throw new InvalidDataException("Tệp này không phải bài PowerPoint (.pptx).");
            if (pres.Root.Name.NamespaceName != NsP) throw new InvalidDataException("Bài PowerPoint này lưu theo kiểu “Strict Open XML”. Thầy cô mở bài bằng PowerPoint, lưu lại thành .pptx thường rồi thử lại.");
            var presRels = Load(zip, presRelsPath);
            var types = Load(zip, "[Content_Types].xml");
            if (presRels == null || types == null) throw new InvalidDataException("Tệp PowerPoint này bị hỏng.");

            long cx = 12192000, cy = 6858000;
            var size = pres.Root.Element(p + "sldSz");
            if (size != null) { long v; if (long.TryParse((string)size.Attribute("cx"), out v)) cx = v; if (long.TryParse((string)size.Attribute("cy"), out v)) cy = v; }

            // the slide layout for the new slides: a blank one if the presentation has it
            var layouts = zip.Entries.Where(e => Regex.IsMatch(e.FullName, "^" + Regex.Escape(prefix) + @"slideLayouts/[^/]+\.xml$")).Select(e => e.FullName).OrderBy(n => n.Length).ThenBy(n => n, StringComparer.Ordinal).ToList();
            if (layouts.Count == 0) throw new InvalidDataException("Bài PowerPoint này không có bố cục trang chiếu nào.");
            string layout = layouts.FirstOrDefault(n => { var d = Load(zip, n); return d != null && (string)d.Root.Attribute("type") == "blank"; }) ?? layouts[0];
            string layoutTarget = "../slideLayouts/" + layout.Substring(layout.LastIndexOf('/') + 1);

            int slideNo = zip.Entries.Select(e => Regex.Match(e.FullName, "^" + Regex.Escape(prefix) + @"slides/slide(\d+)\.xml$")).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value, Inv)).DefaultIfEmpty(0).Max();
            var list = pres.Root.Element(p + "sldIdLst");
            if (list == null)
            {
                list = new XElement(p + "sldIdLst");
                var before = new[] { "handoutMasterIdLst", "notesMasterIdLst", "sldMasterIdLst" }.Select(n => pres.Root.Element(p + n)).FirstOrDefault(e => e != null);
                if (before != null) before.AddAfterSelf(list); else pres.Root.AddFirst(list);
            }
            long nextId = list.Elements(p + "sldId").Select(e => { long v; return long.TryParse((string)e.Attribute("id"), out v) ? v : 0; }).DefaultIfEmpty(255).Max() + 1;
            var usedRel = new HashSet<string>(presRels.Root.Elements(rel + "Relationship").Select(e => (string)e.Attribute("Id")));
            string stamp = Guid.NewGuid().ToString("N").Substring(0, 10);   // never the name of a picture added earlier

            if (!types.Root.Elements(ct + "Default").Any(e => string.Equals((string)e.Attribute("Extension"), "jpeg", StringComparison.OrdinalIgnoreCase)))
                types.Root.AddFirst(new XElement(ct + "Default", new XAttribute("Extension", "jpeg"), new XAttribute("ContentType", "image/jpeg")));

            for (int i = 0; i < pages.Count; i++)
            {
                slideNo++;
                string slideName = "slide" + slideNo + ".xml";
                string media = "phantrang-" + stamp + "-" + (i + 1) + ".jpeg";
                PutBytes(zip, prefix + "media/" + media, pages[i].Jpeg);
                Put(zip, prefix + "slides/" + slideName, Slide(pages[i], cx, cy, i + 1));
                Put(zip, prefix + "slides/_rels/" + slideName + ".rels", Rels(Rel("rId1", "slideLayout", layoutTarget), Rel("rId2", "image", "../media/" + media)));
                types.Root.Add(new XElement(ct + "Override", new XAttribute("PartName", "/" + prefix + "slides/" + slideName), new XAttribute("ContentType", CtSlide)));

                string id;
                int n = 1;
                do id = "rIdPT" + n++; while (usedRel.Contains(id));
                usedRel.Add(id);
                presRels.Root.Add(new XElement(rel + "Relationship", new XAttribute("Id", id), new XAttribute("Type", RelBase + "slide"), new XAttribute("Target", "slides/" + slideName)));
                list.Add(new XElement(p + "sldId", new XAttribute("id", nextId++), new XAttribute(r + "id", id)));
            }
            if (nextId >= 2147483648) throw new InvalidDataException("Bài PowerPoint này có quá nhiều trang chiếu.");

            Save(zip, presPath, pres);
            Save(zip, presRelsPath, presRels);
            Save(zip, "[Content_Types].xml", types);
        }

        // ---------------- parts ----------------

        /// <summary>A slide holding one picture, fitted inside cx x cy (EMU) and centred.</summary>
        static string Slide(PdfPage page, long cx, long cy, int n)
        {
            double pic = page.PixelWidth / (double)Math.Max(1, page.PixelHeight), slide = cx / (double)cy;
            long w = pic >= slide ? cx : (long)Math.Round(cy * pic), h = pic >= slide ? (long)Math.Round(cx / pic) : cy;
            long x = (cx - w) / 2, y = (cy - h) / 2;
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<p:sld xmlns:a=\"" + NsA + "\" xmlns:r=\"" + NsR + "\" xmlns:p=\"" + NsP + "\">" +
                "<p:cSld><p:spTree>" + GroupProps +
                "<p:pic><p:nvPicPr><p:cNvPr id=\"2\" name=\"Trang bảng " + n + "\"/><p:cNvPicPr><a:picLocks noChangeAspect=\"1\"/></p:cNvPicPr><p:nvPr/></p:nvPicPr>" +
                "<p:blipFill><a:blip r:embed=\"rId2\"/><a:stretch><a:fillRect/></a:stretch></p:blipFill>" +
                "<p:spPr><a:xfrm><a:off x=\"" + x + "\" y=\"" + y + "\"/><a:ext cx=\"" + w + "\" cy=\"" + h + "\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></p:spPr></p:pic>" +
                "</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sld>";
        }

        const string GroupProps = "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>" +
            "<p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"0\" cy=\"0\"/></a:xfrm></p:grpSpPr>";

        static readonly string Master = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<p:sldMaster xmlns:a=\"" + NsA + "\" xmlns:r=\"" + NsR + "\" xmlns:p=\"" + NsP + "\">" +
            "<p:cSld><p:bg><p:bgPr><a:solidFill><a:srgbClr val=\"FFFFFF\"/></a:solidFill><a:effectLst/></p:bgPr></p:bg><p:spTree>" + GroupProps + "</p:spTree></p:cSld>" +
            "<p:clrMap bg1=\"lt1\" tx1=\"dk1\" bg2=\"lt2\" tx2=\"dk2\" accent1=\"accent1\" accent2=\"accent2\" accent3=\"accent3\" accent4=\"accent4\" accent5=\"accent5\" accent6=\"accent6\" hlink=\"hlink\" folHlink=\"folHlink\"/>" +
            "<p:sldLayoutIdLst><p:sldLayoutId id=\"2147483649\" r:id=\"rId1\"/></p:sldLayoutIdLst></p:sldMaster>";

        static readonly string Layout = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<p:sldLayout xmlns:a=\"" + NsA + "\" xmlns:r=\"" + NsR + "\" xmlns:p=\"" + NsP + "\" type=\"blank\" preserve=\"1\">" +
            "<p:cSld name=\"Blank\"><p:spTree>" + GroupProps + "</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sldLayout>";

        static string Fill { get { return "<a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill>"; } }

        static readonly string Theme1 = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<a:theme xmlns:a=\"" + NsA + "\" name=\"Phấn Trắng\"><a:themeElements>" +
            "<a:clrScheme name=\"Phấn Trắng\"><a:dk1><a:sysClr val=\"windowText\" lastClr=\"000000\"/></a:dk1><a:lt1><a:sysClr val=\"window\" lastClr=\"FFFFFF\"/></a:lt1>" +
            "<a:dk2><a:srgbClr val=\"1F4D3A\"/></a:dk2><a:lt2><a:srgbClr val=\"F4F1E8\"/></a:lt2>" +
            "<a:accent1><a:srgbClr val=\"2E7D32\"/></a:accent1><a:accent2><a:srgbClr val=\"F2C14E\"/></a:accent2><a:accent3><a:srgbClr val=\"D64545\"/></a:accent3>" +
            "<a:accent4><a:srgbClr val=\"3A7BD5\"/></a:accent4><a:accent5><a:srgbClr val=\"8E5BC9\"/></a:accent5><a:accent6><a:srgbClr val=\"E07A9B\"/></a:accent6>" +
            "<a:hlink><a:srgbClr val=\"0563C1\"/></a:hlink><a:folHlink><a:srgbClr val=\"954F72\"/></a:folHlink></a:clrScheme>" +
            "<a:fontScheme name=\"Phấn Trắng\"><a:majorFont><a:latin typeface=\"Segoe UI\"/><a:ea typeface=\"\"/><a:cs typeface=\"\"/></a:majorFont>" +
            "<a:minorFont><a:latin typeface=\"Segoe UI\"/><a:ea typeface=\"\"/><a:cs typeface=\"\"/></a:minorFont></a:fontScheme>" +
            "<a:fmtScheme name=\"Phấn Trắng\"><a:fillStyleLst>" + Fill + Fill + Fill + "</a:fillStyleLst>" +
            "<a:lnStyleLst><a:ln w=\"6350\">" + Fill + "</a:ln><a:ln w=\"12700\">" + Fill + "</a:ln><a:ln w=\"19050\">" + Fill + "</a:ln></a:lnStyleLst>" +
            "<a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst>" +
            "<a:bgFillStyleLst>" + Fill + Fill + Fill + "</a:bgFillStyleLst></a:fmtScheme></a:themeElements></a:theme>";

        static string Core(string title)
        {
            string now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", Inv);
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" " +
                "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                "<dc:title>" + SecurityElementEscape(title ?? "") + "</dc:title><dc:creator>Phấn Trắng</dc:creator>" +
                "<dcterms:created xsi:type=\"dcterms:W3CDTF\">" + now + "</dcterms:created><dcterms:modified xsi:type=\"dcterms:W3CDTF\">" + now + "</dcterms:modified></cp:coreProperties>";
        }

        static string SecurityElementEscape(string s) { return System.Security.SecurityElement.Escape(s); }

        static string Rel(string id, string type, string target) { return "<Relationship Id=\"" + id + "\" Type=\"" + RelBase + type + "\" Target=\"" + target + "\"/>"; }

        static string Rels(params string[] items)
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<Relationships xmlns=\"" + NsRel + "\">" + string.Concat(items) + "</Relationships>";
        }

        // ---------------- zip helpers ----------------

        static ZipArchiveEntry Find(ZipArchive zip, string name)
        {
            return zip.GetEntry(name) ?? zip.Entries.FirstOrDefault(e => string.Equals(e.FullName, name, StringComparison.OrdinalIgnoreCase));
        }

        static XDocument Load(ZipArchive zip, string name)
        {
            var e = Find(zip, name);
            if (e == null) return null;
            try
            {
                using (var s = e.Open())
                using (var reader = XmlReader.Create(s, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                    return XDocument.Load(reader);
            }
            catch (XmlException ex) { throw new InvalidDataException("Tệp PowerPoint này bị hỏng.", ex); }
        }

        static void PutBytes(ZipArchive zip, string name, byte[] data)
        {
            if (zip.Mode != ZipArchiveMode.Create) { var old = Find(zip, name); if (old != null) old.Delete(); }
            using (var s = zip.CreateEntry(name, CompressionLevel.Optimal).Open()) s.Write(data, 0, data.Length);
        }

        static void Put(ZipArchive zip, string name, string xml) { PutBytes(zip, name, Utf8.GetBytes(xml)); }

        static void Save(ZipArchive zip, string name, XDocument doc)
        {
            using (var ms = new MemoryStream())
            {
                using (var w = XmlWriter.Create(ms, new XmlWriterSettings { Encoding = Utf8 })) doc.Save(w);
                var old = Find(zip, name);
                string keep = old != null ? old.FullName : name;
                PutBytes(zip, keep, ms.ToArray());
            }
        }

        /// <summary>Puts the finished temporary file in place of 'full' (an open copy in PowerPoint makes this fail with IOException).</summary>
        static void Swap(string tmp, string full)
        {
            if (File.Exists(full)) File.Replace(tmp, full, null);
            else File.Move(tmp, full);
        }
    }
}
