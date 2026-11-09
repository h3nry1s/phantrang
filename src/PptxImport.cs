using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PhanTrang
{
    /// <summary>
    /// Opens a PowerPoint presentation as board pages, one picture per slide, so the teacher can write over the
    /// slides. Drawing a .pptx faithfully needs PowerPoint itself, so the slides are exported by the PowerPoint
    /// (or WPS Presentation) already installed on the machine, through its automation interface. Nothing ships
    /// with Phấn Trắng, and the teacher's file is opened read-only and left untouched.
    /// </summary>
    public static class PptxImport
    {
        public const string Filter = "Bài PowerPoint (*.pptx; *.ppt; *.ppsx; *.pps)|*.pptx;*.ppt;*.ppsx;*.pps";

        // PowerPoint first; WPS Presentation speaks the same object model
        static readonly string[][] Apps = { new[] { "PowerPoint.Application", "POWERPNT" }, new[] { "KWPP.Application", "wpp" } };

        const int MsoTrue = -1, MsoFalse = 0, PpAlertsNone = 1;

        /// <summary>
        /// The slides of 'path' as board pages sized for a w x h screen. Hidden slides are left out.
        /// Throws InvalidDataException with a message for the teacher when it cannot be done.
        /// </summary>
        public static List<Sheet> Load(string path, int w, int h)
        {
            if (!File.Exists(path)) throw new FileNotFoundException(path);
            string dir = Path.Combine(Path.GetTempPath(), "PhanTrang-slides-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var files = ExportSlides(Path.GetFullPath(path), dir, w, h);
                if (files.Count == 0) throw new InvalidDataException("Bài PowerPoint này không có trang chiếu nào (hoặc tất cả đều đang ẩn).");
                if (files.Count > Lesson.MaxPages) files = files.Take(Lesson.MaxPages).ToList();
                double now = Clock.Now - 5;   // already there: no pop-in animation
                var pages = new List<Sheet>();
                foreach (var f in files)
                {
                    var pic = PictureMark.FromBytes(File.ReadAllBytes(f));
                    pic.Rect = Fit(pic.Img.Width, pic.Img.Height, w, h);
                    pic.Start = now; pic.Done = now;
                    var page = new Sheet { Board = "white" };
                    page.Marks.Add(pic);
                    pages.Add(page);
                }
                return pages;
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (Exception) { }
            }
        }

        /// <summary>The largest rectangle with the slide's proportions that fits the screen, centred.</summary>
        public static RectangleF Fit(int iw, int ih, int w, int h)
        {
            float k = Math.Min(w / (float)iw, h / (float)ih);
            float rw = iw * k, rh = ih * k;
            return new RectangleF((w - rw) / 2, (h - rh) / 2, rw, rh);
        }

        static List<string> ExportSlides(string path, string dir, int w, int h)
        {
            Exception lastError = null;
            foreach (var a in Apps)
            {
                var type = Type.GetTypeFromProgID(a[0], false);
                if (type == null) continue;
                bool wasRunning = Process.GetProcessesByName(a[1]).Length > 0;
                object app = null, presentations = null, pres = null;
                try
                {
                    app = Activator.CreateInstance(type);
                    try { Set(app, "DisplayAlerts", PpAlertsNone); } catch (Exception) { }
                    presentations = Get(app, "Presentations");
                    // Open(FileName, ReadOnly, Untitled, WithWindow): read-only, out of sight
                    pres = Call(presentations, "Open", path, MsoTrue, MsoFalse, MsoFalse);
                    return ExportFrom(pres, dir, w, h);
                }
                catch (InvalidDataException) { throw; }
                catch (Exception e) { lastError = e; Log.Write("mở PowerPoint qua " + a[0] + ": " + e); }
                finally
                {
                    if (pres != null) { try { Call(pres, "Close"); } catch (Exception) { } Release(pres); }
                    if (app != null && !wasRunning)
                    {
                        try { if (Convert.ToInt32(Get(presentations, "Count")) == 0) Call(app, "Quit"); } catch (Exception) { }
                    }
                    Release(presentations);
                    Release(app);
                }
            }
            if (lastError == null)
                throw new InvalidDataException("Máy này chưa có PowerPoint hoặc WPS Presentation. Phấn Trắng cần một trong hai để đọc bài PowerPoint.");
            throw new InvalidDataException("PowerPoint không mở được bài này. Có thể bài có mật khẩu hoặc bị hỏng; thầy cô thử mở bài trong PowerPoint xem sao.");
        }

        static List<string> ExportFrom(object pres, string dir, int w, int h)
        {
            var files = new List<string>();
            object slides = Get(pres, "Slides");
            try
            {
                // export at the slide's own proportions, as large as fits the screen
                float sw = Convert.ToSingle(Get(Get(pres, "PageSetup"), "SlideWidth"));
                float sh = Convert.ToSingle(Get(Get(pres, "PageSetup"), "SlideHeight"));
                var r = Fit((int)Math.Round(sw * 100), (int)Math.Round(sh * 100), Math.Min(w, PictureMark.MaxSide), Math.Min(h, PictureMark.MaxSide));
                int ew = Math.Max(1, (int)Math.Round(r.Width)), eh = Math.Max(1, (int)Math.Round(r.Height));
                int n = Convert.ToInt32(Get(slides, "Count"));
                for (int i = 1; i <= n && files.Count < Lesson.MaxPages; i++)
                {
                    object slide = Call(slides, "Item", i);
                    try
                    {
                        bool hidden = false;
                        try { hidden = Convert.ToInt32(Get(Get(slide, "SlideShowTransition"), "Hidden")) == MsoTrue; } catch (Exception) { }
                        if (hidden) continue;
                        string file = Path.Combine(dir, "slide" + i.ToString("D3") + ".png");
                        Call(slide, "Export", file, "PNG", ew, eh);
                        if (File.Exists(file)) files.Add(file);
                    }
                    finally { Release(slide); }
                }
            }
            finally { Release(slides); }
            return files;
        }

        static object Get(object o, string name) { return o.GetType().InvokeMember(name, BindingFlags.GetProperty, null, o, null); }
        static void Set(object o, string name, object v) { o.GetType().InvokeMember(name, BindingFlags.SetProperty, null, o, new[] { v }); }
        static object Call(object o, string name, params object[] args) { return o.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, o, args); }
        static void Release(object o) { if (o != null && Marshal.IsComObject(o)) try { Marshal.FinalReleaseComObject(o); } catch (Exception) { } }
    }
}
