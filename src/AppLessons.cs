using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PhanTrang
{
    /// <summary>Typing, prepared lessons (new, open, save, export), pictures and the spare file.</summary>
    sealed partial class TeacherApp
    {
        TypePanel typePanel;

        // ---- typing ----

        void OpenTyping()
        {
            if (!on) return;
            if (typePanel == null || typePanel.IsDisposed)
            {
                double size = Settings.TypePanelSize;
                typePanel = new TypePanel(Math.Min(screenScale * size, (work.Width - 16) / (double)TypePanel.PanelW), size);   // never wider than the screen
                typePanel.TextTyped += text => Stage.ShowTyping(text, Settings.TextFont, TypingPx);
                typePanel.LookChanged += () => Stage.ShowTyping(typePanel.Typed, Settings.TextFont, TypingPx);
                typePanel.Commit += () => { Stage.CommitTyping(); typePanel.ClearText(); Refresh(); };
                typePanel.RemoveLast += () => { Stage.RemoveLastLine(); Refresh(); };
                typePanel.CloseRequested += () => SetTool("pointer");
                typePanel.Moved += RememberTypePanelPlace;
                typePanel.Resized += factor =>
                {
                    RememberTypePanelPlace();
                    Settings.TypePanelSize = Math.Max(0.6, Math.Min(1.5, typePanel.SizeSetting * factor));
                    Post(RebuildTypePanel);   // not from inside the panel's own event
                };
            }
            Stage.ShowTyping(typePanel.Typed, Settings.TextFont, TypingPx);
            var caret = Stage.Caret;
            int caretTop = layer.Monitor.Top + (int)caret.Y;
            typePanel.Open(work, toolbar.ScreenTop, toolbar.ScreenBottom, caretTop, caretTop + (int)(TypingPx * 1.3f));
            UpdateInput();
            Frame();
        }

        void RememberTypePanelPlace()
        {
            if (typePanel == null || typePanel.IsDisposed || work.Width <= 0 || work.Height <= 0) return;
            Settings.TypePanelX = Math.Max(0, (typePanel.Left - work.Left) / (double)work.Width);
            Settings.TypePanelY = Math.Max(0, (typePanel.Top - work.Top) / (double)work.Height);
        }

        /// <summary>Builds the typing panel again at the size now chosen, keeping what was typed.</summary>
        void RebuildTypePanel()
        {
            if (typePanel == null || typePanel.IsDisposed) return;
            bool wasOpen = typePanel.Visible;
            string text = typePanel.Typed;
            var old = typePanel;
            typePanel = null;
            old.Hide();
            old.Dispose();
            if (!wasOpen || !on || Settings.Tool != "type") return;
            OpenTyping();
            if (typePanel != null) { typePanel.SetText(text); Stage.ShowTyping(text, Settings.TextFont, TypingPx); }
        }

        /// <summary>Hides the typing panel; a line typed but not yet put down goes onto the page.</summary>
        void CloseTyping()
        {
            if (typePanel != null && !typePanel.IsDisposed)
            {
                if (typePanel.Typed.Trim().Length > 0) Stage.CommitTyping();
                typePanel.ClearText();
                typePanel.Hide();
            }
            Stage.StopTyping();
            UpdateInput();
            if (on) Frame();
        }

        void RefreshTyping()
        {
            if (typePanel != null && typePanel.Visible) Stage.ShowTyping(typePanel.Typed, Settings.TextFont, TypingPx);
        }

        // ---- dialogs over the board ----

        /// <summary>
        /// Shows a dialog above the board. Meanwhile the Windows pointer is back and clicks are not captured.
        /// The invisible owner keeps the dialog on the monitor in use and above the topmost layer.
        /// </summary>
        T OverBoard<T>(Func<IWin32Window, T> show)
        {
            dialogOpen = true;
            UpdateInput();
            if (on) Frame();
            using (var owner = new Form { ShowInTaskbar = false, TopMost = true, FormBorderStyle = FormBorderStyle.None, Size = new Size(1, 1), StartPosition = FormStartPosition.Manual, Opacity = 0 })
            {
                owner.Location = new Point(work.Left + work.Width / 2, work.Top + work.Height / 4);
                owner.Show();
                try { return show(owner); }
                finally
                {
                    owner.Close();
                    dialogOpen = false;
                    UpdateInput();
                    if (on) { Frame(); Restack(); }
                }
            }
        }

        void Say(string text, bool problem = false)
        {
            OverBoard(o => MessageBox.Show(o, text, "Phấn Trắng", MessageBoxButtons.OK, problem ? MessageBoxIcon.Warning : MessageBoxIcon.Information));
        }

        /// <summary>Export dialogs start where the last export went, else in Documents.</summary>
        static string ExportFolder
        {
            get
            {
                string last = Settings.ExportFolder;
                try { if (last.Length > 0 && Directory.Exists(last)) return last; }
                catch (ArgumentException) { }
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }

        static void RememberFolder(string path)
        {
            try { Settings.ExportFolder = Path.GetDirectoryName(Path.GetFullPath(path)); }
            catch (ArgumentException) { }
        }

        /// <summary>The screen lessons are measured against: the one in use, or the one under the mouse while off.</summary>
        RECT LessonScreen { get { return layer.Monitor.Width > 0 ? layer.Monitor : Win32.ScreenAtCursor().Bounds; } }

        string BoardToSave { get { return Stage.Book.OnBoard ? Stage.Board : Settings.BoardKind; } }

        void MinimizeSettings() { if (settingsWin != null && !settingsWin.IsDisposed) settingsWin.WindowState = FormWindowState.Minimized; }

        static string Today { get { return DateTime.Now.ToString("dd-MM-yyyy"); } }

        // ---- lessons ----

        /// <summary>
        /// Starts a new lesson: blank board pages, ready to type. The pages already there are not lost: they go to a
        /// spare copy that the tray menu can open again ("Bản tự lưu …").
        /// </summary>
        public void NewLesson()
        {
            if (Stage.Book.HasLesson)
            {
                var answer = OverBoard(o => MessageBox.Show(o, "Bắt đầu bài mới?\nCác trang đang có được cất vào bản tự lưu, mở lại ở khay hệ thống › Bài giảng.", "Bài mới", MessageBoxButtons.OKCancel, MessageBoxIcon.Question));
                if (answer != DialogResult.OK) return;
                AutoSave();
                Lesson.KeepEarlierAutoSave();
            }
            MinimizeSettings();
            if (!on) TurnOn();
            if (!on) return;
            Usage.Count("lesson.new");
            Stage.LoadPages(new List<Sheet>(), Settings.BoardKind);
            SetTool("type");
            Refresh();
        }

        static string WhyNotOpened(Exception e)
        {
            if (e is InvalidDataException) return e.Message;
            if (e is FileNotFoundException || e is DirectoryNotFoundException) return "Không tìm thấy tệp. Có thể tệp đã bị xoá.";
            if (e is UnauthorizedAccessException) return "Windows không cho đọc tệp này.";
            return "Tệp đang bị chương trình khác giữ.";
        }

        /// <summary>Opens a spare copy Phấn Trắng saved by itself and shows its first page.</summary>
        public void OpenSpare(string path)
        {
            var screen = LessonScreen;
            List<Sheet> pages;
            try { pages = Lesson.Load(path, screen.Width, screen.Height); }
            catch (Exception e)
            {
                Log.Write("mở bản tự lưu: " + e);
                Say("Không mở được bản tự lưu.\n" + WhyNotOpened(e), true);
                return;
            }
            MinimizeSettings();
            if (!on) TurnOn();
            if (!on) return;
            Usage.Count("lesson.spare");
            Stage.LoadPages(pages, Settings.BoardKind);
            if (Settings.Tool == "type") SetTool("pointer");
            RefreshTyping();
            Refresh();
        }

        /// <summary>
        /// Opens a PowerPoint presentation as a new lesson: each slide becomes a white board page with the slide as
        /// its picture, ready to write over. Needs PowerPoint or WPS on the machine (see PptxImport).
        /// </summary>
        public void OpenPptx()
        {
            string path = OverBoard(o =>
            {
                using (var d = new OpenFileDialog { Filter = PptxImport.Filter, Title = "Mở bài PowerPoint để viết lên", InitialDirectory = ExportFolder })
                    return d.ShowDialog(o) == DialogResult.OK ? d.FileName : null;
            });
            if (path == null) return;
            RememberFolder(path);
            var screen = LessonScreen;
            List<Sheet> pages;
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                pages = PptxImport.Load(path, screen.Width, screen.Height);
            }
            catch (Exception e)
            {
                Log.Write("mở PowerPoint: " + e);
                Say("Không mở được bài PowerPoint.\n" + WhyNotOpened(e), true);
                return;
            }
            finally { Cursor.Current = Cursors.Default; }
            MinimizeSettings();
            if (!on) TurnOn();
            if (!on) return;
            if (Stage.Book.HasLesson) { AutoSave(); Lesson.KeepEarlierAutoSave(); }   // the pages being replaced stay in a spare copy
            Usage.Count("lesson.pptx");
            Stage.LoadPages(pages, "white");
            if (Settings.Tool == "type" || Settings.Tool == "image") SetTool("draw");
            RefreshTyping();
            Refresh();
            Say("Đã mở " + pages.Count + " trang chiếu thành " + pages.Count + " trang bảng. Lật trang ở thanh công cụ.");
        }

        string AskSave(string filter, string title, string name, string ext)
        {
            return OverBoard(o =>
            {
                using (var d = new SaveFileDialog { Filter = filter, Title = title, InitialDirectory = ExportFolder, FileName = name, AddExtension = true, DefaultExt = ext })
                    return d.ShowDialog(o) == DialogResult.OK ? d.FileName : null;
            });
        }

        public void ExportImages()
        {
            if (!Stage.Book.HasLesson) { Say("Bảng chưa có gì để xuất."); return; }
            string path = AskSave("Ảnh PNG (*.png)|*.png", "Xuất các trang bảng thành ảnh", "Bảng " + Today, "png");
            if (path == null) return;
            RememberFolder(path);
            var screen = LessonScreen;
            try
            {
                Usage.Count("export.png");
                int n = Lesson.ExportImages(path, Stage.BoardPages, screen.Width, screen.Height, Stage.Board);
                Say("Đã lưu " + n + " ảnh vào thư mục\n" + Path.GetDirectoryName(path));
            }
            catch (Exception e)
            {
                Log.Write("xuất ảnh: " + e);
                Say("Không lưu được ảnh vào chỗ này. Thầy cô thử chọn thư mục khác.", true);
            }
        }

        /// <summary>The whole lesson as one PDF: to print, hand out, or send to a colleague who does not use Phấn Trắng.</summary>
        public void ExportPdf()
        {
            if (!Stage.Book.HasLesson) { Say("Bảng chưa có gì để xuất."); return; }
            string path = AskSave("Tệp PDF (*.pdf)|*.pdf", "Xuất bài giảng thành PDF", "Bài giảng " + Today, "pdf");
            if (path == null) return;
            RememberFolder(path);
            var screen = LessonScreen;
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                Usage.Count("export.pdf");
                int n = Lesson.ExportPdf(path, Stage.BoardPages, screen.Width, screen.Height, Stage.Board, Path.GetFileNameWithoutExtension(path));
                Say("Đã xuất " + n + " trang vào tệp\n" + path);
            }
            catch (Exception e)
            {
                Log.Write("xuất PDF: " + e);
                Say(e is IOException ? "Tệp PDF này đang mở ở chương trình khác. Thầy cô đóng tệp đó rồi xuất lại, hoặc đặt tên khác." : "Không xuất được PDF vào chỗ này. Thầy cô thử chọn thư mục khác.", true);
            }
            finally { Cursor.Current = Cursors.Default; }
        }

        /// <summary>
        /// PowerPoint features (open a presentation to write over, export / add to a presentation) are held back for
        /// now: their buttons, menu entries and help text are hidden while this is false. The code stays in place.
        /// </summary>
        public static bool PowerPointOn { get { return false; } }

        /// <summary>
        /// The lesson as PowerPoint slides (one picture per page), to present: a new .pptx, or (append) added after
        /// the slides of a presentation the teacher already has.
        /// </summary>
        public void ExportPptx(bool append)
        {
            if (!Stage.Book.HasLesson) { Say("Bảng chưa có gì để xuất."); return; }
            const string filter = "Bài PowerPoint (*.pptx)|*.pptx";
            string path = append
                ? OverBoard(o =>
                {
                    using (var d = new OpenFileDialog { Filter = filter, Title = "Chọn bài PowerPoint để thêm các trang bảng vào cuối", InitialDirectory = ExportFolder })
                        return d.ShowDialog(o) == DialogResult.OK ? d.FileName : null;
                })
                : AskSave(filter, "Xuất bài giảng thành PowerPoint", "Bài giảng " + Today, "pptx");
            if (path == null) return;
            RememberFolder(path);
            var screen = LessonScreen;
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                Usage.Count(append ? "export.addpptx" : "export.pptx");
                int n = Lesson.ExportPptx(path, Stage.BoardPages, screen.Width, screen.Height, Stage.Board, append, Path.GetFileNameWithoutExtension(path));
                Say((append ? "Đã thêm " + n + " trang chiếu vào cuối bài\n" : "Đã xuất " + n + " trang chiếu vào tệp\n") + path);
            }
            catch (Exception e)
            {
                Log.Write("xuất PowerPoint: " + e);
                string why = e is InvalidDataException ? e.Message
                    : e is IOException ? "Bài PowerPoint này đang mở. Thầy cô đóng bài trong PowerPoint rồi thử lại."
                    : "Không ghi được vào chỗ này. Thầy cô thử chọn thư mục khác.";
                Say("Không xuất được PowerPoint.\n" + why, true);
            }
            finally { Cursor.Current = Cursors.Default; }
        }

        /// <summary>Picks a picture; it then follows the pointer on the board until the teacher clicks where it goes.</summary>
        public void AddImage()
        {
            string path = OverBoard(o =>
            {
                using (var d = new OpenFileDialog { Filter = "Ảnh (*.png; *.jpg; *.jpeg; *.bmp; *.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif", Title = "Thêm ảnh vào trang", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) })
                    return d.ShowDialog(o) == DialogResult.OK ? d.FileName : null;
            });
            if (path == null) return;
            PictureMark picture;
            try
            {
                if (new FileInfo(path).Length > PictureMark.MaxFileBytes) { Say("Ảnh này lớn quá (trên 30 MB). Thầy cô chọn ảnh nhỏ hơn nhé.", true); return; }
                picture = PictureMark.FromBytes(File.ReadAllBytes(path));
            }
            catch (InvalidDataException e) { Say(e.Message, true); return; }
            catch (Exception e) { Log.Write("thêm ảnh: " + e); Say("Không mở được ảnh này.", true); return; }
            MinimizeSettings();
            if (!on) TurnOn();
            if (!on) return;
            if (!Stage.Book.OnBoard) SetBoard(Stage.Book.PageBoard ?? Settings.BoardKind);   // pictures go on the board only
            CloseTyping();
            Usage.Count("image.add");
            Stage.Pictures.Begin(picture, ToLayer(Win32.CursorPos()));
            UpdateInput();
            toolbar.Coach(new[] { new[] { "Bấm", "vào chỗ muốn đặt ảnh" }, new[] { "Esc", "để thôi" } });
            Refresh();
        }

        // ---- the spare file ----

        bool earlierSpareKept;

        /// <summary>
        /// Unsaved board pages are written to a spare file whenever Phấn Trắng is switched off or closed, in case
        /// that happened by mistake. The first time in a session, the previous session's spare is kept aside first.
        /// </summary>
        void AutoSave()
        {
            try
            {
                if (!Stage.Book.HasLesson) return;
                if (!earlierSpareKept) { Lesson.KeepEarlierAutoSave(); earlierSpareKept = true; }
                var screen = LessonScreen;
                Lesson.Save(Lesson.AutoSavePath, Stage.BoardPages, screen.Width, screen.Height, BoardToSave);
            }
            catch (Exception e) { Log.Write("tự lưu: " + e.Message); }
        }
    }
}
