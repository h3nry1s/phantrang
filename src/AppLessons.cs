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
                typePanel = new TypePanel(Math.Min(screenScale, (work.Width - 16) / (double)TypePanel.PanelW));   // never wider than the screen
                typePanel.TextTyped += text => Stage.ShowTyping(text, Settings.TextFont, TypingPx);
                typePanel.LookChanged += () => Stage.ShowTyping(typePanel.Typed, Settings.TextFont, TypingPx);
                typePanel.Commit += () => { Stage.CommitTyping(); typePanel.ClearText(); Refresh(); };
                typePanel.RemoveLast += () => { Stage.RemoveLastLine(); Refresh(); };
                typePanel.CloseRequested += () => SetTool("pointer");
            }
            Stage.ShowTyping(typePanel.Typed, Settings.TextFont, TypingPx);
            typePanel.Open(work, toolbar.ScreenTop);
            UpdateInput();
            Frame();
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

        /// <summary>Open and save dialogs start where the last lesson was, else in Documents.</summary>
        string LessonFolder
        {
            get
            {
                var last = Settings.RecentLessons.FirstOrDefault();
                return last != null ? Path.GetDirectoryName(last) : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }

        /// <summary>The screen lessons are measured against: the one in use, or the one under the mouse while off.</summary>
        RECT LessonScreen { get { return layer.Monitor.Width > 0 ? layer.Monitor : Win32.ScreenAtCursor().Bounds; } }

        string BoardToSave { get { return Stage.Book.OnBoard ? Stage.Board : Settings.BoardKind; } }

        void MinimizeSettings() { if (settingsWin != null && !settingsWin.IsDisposed) settingsWin.WindowState = FormWindowState.Minimized; }

        // ---- lessons ----

        /// <summary>Starts a new lesson: blank board pages, ready to type. Offers to save the open one first.</summary>
        public void NewLesson()
        {
            if (Stage.Book.HasLesson)
            {
                var answer = OverBoard(o => MessageBox.Show(o, "Lưu bài đang mở trước khi soạn bài mới?", "Bài mới", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question));
                if (answer == DialogResult.Cancel || (answer == DialogResult.Yes && !SaveLesson())) return;
            }
            MinimizeSettings();
            if (!on) TurnOn();
            if (!on) return;
            Stage.LoadPages(new List<Sheet>(), Settings.BoardKind);
            SetTool("type");
            Refresh();
        }

        static string WhyNotOpened(Exception e)
        {
            if (e is InvalidDataException) return e.Message;
            if (e is FileNotFoundException || e is DirectoryNotFoundException) return "Không tìm thấy tệp. Có thể tệp đã bị xoá hoặc USB đã rút ra.";
            if (e is UnauthorizedAccessException) return "Windows không cho đọc tệp này.";
            return "Tệp đang bị chương trình khác giữ.";
        }

        /// <summary>Opens a lesson file (asks which one if path is null) and shows its first page.</summary>
        public void OpenLesson(string path)
        {
            if (path == null)
            {
                path = OverBoard(o =>
                {
                    using (var d = new OpenFileDialog { Filter = Lesson.Filter, Title = "Mở bài giảng", InitialDirectory = LessonFolder })
                        return d.ShowDialog(o) == DialogResult.OK ? d.FileName : null;
                });
                if (path == null) return;
            }
            var screen = LessonScreen;
            List<Sheet> pages;
            try { pages = Lesson.Load(path, screen.Width, screen.Height); }
            catch (Exception e)
            {
                Log.Write("mở bài: " + e);
                Say("Không mở được bài giảng.\n" + WhyNotOpened(e), true);
                return;
            }
            if (!IsSpareFile(path)) Settings.AddRecent(path);
            MinimizeSettings();
            if (!on) TurnOn();
            if (!on) return;
            Stage.LoadPages(pages, Settings.BoardKind);
            if (Settings.Tool == "type") SetTool("pointer");
            RefreshTyping();
            Refresh();
        }

        static bool IsSpareFile(string path)
        {
            return Path.GetFullPath(path).StartsWith(Path.GetFullPath(Settings.Dir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Asks where and saves the board pages. False if the teacher cancelled or saving failed.</summary>
        public bool SaveLesson()
        {
            if (!Stage.Book.HasLesson) { Say("Bảng chưa có gì để lưu. Mở bảng, viết lên các trang rồi lưu lại."); return false; }
            string path = OverBoard(o =>
            {
                using (var d = new SaveFileDialog { Filter = Lesson.Filter, Title = "Lưu bài giảng", InitialDirectory = LessonFolder, FileName = "Bài giảng " + DateTime.Now.ToString("dd-MM-yyyy"), AddExtension = true, DefaultExt = "phantrang" })
                    return d.ShowDialog(o) == DialogResult.OK ? d.FileName : null;
            });
            if (path == null) return false;
            var screen = LessonScreen;
            try { Lesson.Save(path, Stage.BoardPages, screen.Width, screen.Height, BoardToSave); }
            catch (Exception e)
            {
                Log.Write("lưu bài: " + e);
                Say("Không lưu được bài giảng vào chỗ này. Thầy cô thử chọn thư mục khác (ví dụ Tài liệu).", true);
                return false;
            }
            Settings.AddRecent(path);
            Refresh();
            return true;
        }

        public void ExportImages()
        {
            if (!Stage.Book.HasLesson) { Say("Bảng chưa có gì để xuất."); return; }
            string path = OverBoard(o =>
            {
                using (var d = new SaveFileDialog { Filter = "Ảnh PNG (*.png)|*.png", Title = "Xuất các trang bảng thành ảnh", InitialDirectory = LessonFolder, FileName = "Bảng " + DateTime.Now.ToString("dd-MM-yyyy"), AddExtension = true, DefaultExt = "png" })
                    return d.ShowDialog(o) == DialogResult.OK ? d.FileName : null;
            });
            if (path == null) return;
            var screen = LessonScreen;
            try
            {
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
            string path = OverBoard(o =>
            {
                using (var d = new SaveFileDialog { Filter = "Tệp PDF (*.pdf)|*.pdf", Title = "Xuất bài giảng thành PDF", InitialDirectory = LessonFolder, FileName = "Bài giảng " + DateTime.Now.ToString("dd-MM-yyyy"), AddExtension = true, DefaultExt = "pdf" })
                    return d.ShowDialog(o) == DialogResult.OK ? d.FileName : null;
            });
            if (path == null) return;
            var screen = LessonScreen;
            try
            {
                Cursor.Current = Cursors.WaitCursor;
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
