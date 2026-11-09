using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PhanTrang
{
    /// <summary>Settings: the sections on the left, the chosen section in the middle, "Thử ngay" on the right.</summary>
    class SettingsWindow : Form
    {
        /// <summary>One section: id, title in the sidebar, icon, and the method that fills it.</summary>
        sealed class Section
        {
            public string Id, Title, Icon;
            public Action<PageBuilder> Fill;
        }

        /// <summary>Adds the parts of a page top to bottom. Rows added one after another share one card until the next heading.</summary>
        sealed class PageBuilder
        {
            readonly FlowLayoutPanel flow;
            readonly float k;
            readonly string icon;
            SettingsCard card;
            public readonly int Width;

            public PageBuilder(FlowLayoutPanel flow, float k, int width, string icon) { this.flow = flow; this.k = k; Width = width; this.icon = icon; }

            int Px(double v) { return (int)Math.Round(v * k); }

            public void Add(Control c) { card = null; flow.Controls.Add(c); }

            public void Title(string text, string sub) { Add(new PageHeader(icon, text, sub, Width, k) { Margin = new Padding(0, 0, 0, Px(6)) }); }

            public void Heading(string text)
            {
                Add(new Label { Text = text.ToUpperInvariant(), UseMnemonic = false, Font = Ui.Section(k), ForeColor = Theme.Green, AutoSize = true, Margin = new Padding(Px(4), Px(22), 0, Px(8)) });
            }

            public void Note(string text)
            {
                Add(new Label { Text = text, UseMnemonic = false, Font = Ui.Caption(k), ForeColor = Theme.Dim, AutoSize = true, MaximumSize = new Size(Width - Px(8), 0), Margin = new Padding(Px(4), Px(4), 0, Px(6)) });
            }

            public void Row(string title, string desc, Control control)
            {
                if (card == null)
                {
                    card = new SettingsCard(Width, k) { Margin = new Padding(0, 0, 0, Px(4)) };
                    flow.Controls.Add(card);
                }
                card.AddRow(title, desc, control);
            }
        }

        readonly TeacherApp app;
        readonly Panel sidebar = new Panel(), body = new Panel();
        readonly TryPanel tryIt;
        readonly List<RoundButton> sectionButtons = new List<RoundButton>();
        readonly List<Section> sections;
        readonly float k;
        string current = "pointer";

        public SettingsWindow(TeacherApp app)
        {
            this.app = app;
            k = DeviceDpi / 96f;
            sections = new List<Section>
            {
                new Section { Id = "general", Title = "Chung", Icon = "gear", Fill = General },
                new Section { Id = "pointer", Title = "Công cụ chỉ", Icon = "mode:point", Fill = PointerSection },
                new Section { Id = "board", Title = "Bảng & chữ", Icon = "board", Fill = BoardSection },
                new Section { Id = "lesson", Title = "Bài giảng", Icon = "newlesson", Fill = LessonSection },
                new Section { Id = "grade", Title = "Chấm bài", Icon = "mode:grade", Fill = GradeSection },
                new Section { Id = "keys", Title = "Phím tắt", Icon = "keyboard", Fill = KeysSection },
                new Section { Id = "about", Title = "Giới thiệu", Icon = "info", Fill = About },
            };

            Text = "Phấn Trắng – Cài đặt";
            AutoScaleMode = AutoScaleMode.None;
            Font = Ui.Body(k);
            BackColor = Theme.Cream;
            ForeColor = Theme.Ink;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Px(1180), Px(760));
            MinimumSize = new Size(Px(980), Px(620));
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch (ArgumentException) { }

            tryIt = new TryPanel(app) { Dock = DockStyle.Fill };
            body.Dock = DockStyle.Fill;
            body.AutoScroll = true;
            body.BackColor = Theme.Cream;
            body.Padding = new Padding(Px(30), Px(24), Px(20), Px(24));
            Controls.Add(body);
            Controls.Add(TryColumn());
            Controls.Add(Sidebar());
            ShowPage(current);
            FormClosed += (s, e) => tryIt.Shutdown();
            HandleCreated += (s, e) => Ui.PaperCaption(Handle);
        }

        int Px(double v) { return (int)Math.Round(v * k); }
        int ColumnWidth { get { return Px(540); } }

        RoundButton Button(string text, bool primary)
        {
            return new RoundButton(text, primary ? RoundButton.Kind.Primary : RoundButton.Kind.Secondary, k) { Margin = new Padding(0, 0, Px(8), 0) };
        }

        string OnOffLabel { get { return app.IsActive ? "Tắt Phấn Trắng" : "Bật cho cả màn hình"; } }

        /// <summary>The right column: a heading, the try-it page in a thin frame, and the big on/off button.</summary>
        Panel TryColumn()
        {
            var column = new Panel { Dock = DockStyle.Right, Width = Px(360), Padding = new Padding(Px(4), Px(22), Px(22), Px(22)), BackColor = Theme.Cream };
            var heading = new Panel { Dock = DockStyle.Top, Height = Px(58), BackColor = Theme.Cream };
            heading.Paint += (s, e) =>
            {
                using (var f = Fonts.Get("uiBold", Px(16))) Ui.Text(e.Graphics, "Thử ngay", f, Theme.Ink, new Rectangle(0, Px(2), heading.Width, Px(24)), TextFormatFlags.Left);
                using (var f = Ui.Caption(k)) Ui.Text(e.Graphics, "Rê chuột, bấm để chạm, kéo để vẽ.", f, Theme.Dim, new Rectangle(0, Px(28), heading.Width, Px(20)), TextFormatFlags.Left);
            };
            var power = Button(OnOffLabel, true);
            power.Dock = DockStyle.Fill;
            power.Click += (s, e) => app.ToggleFromSettings();
            Action follow = () => { if (!IsDisposed) power.Text = OnOffLabel; };
            app.ActiveChanged += follow;
            FormClosed += (s, e) => app.ActiveChanged -= follow;
            var footer = new Panel { Dock = DockStyle.Bottom, Height = Px(64), Padding = new Padding(0, Px(12), 0, 0), BackColor = Theme.Cream };
            footer.Controls.Add(power);
            var frame = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BackColor = Theme.Line };
            frame.Controls.Add(tryIt);
            column.Controls.Add(frame);
            column.Controls.Add(footer);
            column.Controls.Add(heading);
            return column;
        }

        /// <summary>The left column: the mark, the sections, and a tip about the on/off shortcut.</summary>
        Panel Sidebar()
        {
            sidebar.Dock = DockStyle.Left;
            sidebar.Width = Px(232);
            sidebar.BackColor = Theme.Sidebar;
            sidebar.Padding = new Padding(Px(14), Px(18), Px(14), Px(14));

            var brand = new Panel { Dock = DockStyle.Top, Height = Px(64), BackColor = sidebar.BackColor };
            brand.Paint += (s, e) =>
            {
                Draw.Logo(e.Graphics, new RectangleF(Px(4), Px(6), Px(36), Px(36)));
                using (var f = Fonts.Get("uiBold", Px(18)))
                    Ui.Text(e.Graphics, "Phấn Trắng", f, Theme.Green, new Rectangle(Px(48), Px(6), brand.Width - Px(48), Px(36)), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            };

            var list = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = sidebar.BackColor };
            foreach (var section in sections)
            {
                string id = section.Id;
                var b = new RoundButton(section.Title, RoundButton.Kind.Nav, k) { Width = Px(208), Margin = new Padding(0, 0, 0, Px(4)), Tag = id, Icon = section.Icon, BackColor = sidebar.BackColor };
                b.Click += (s, e) => ShowPage(id);
                sectionButtons.Add(b);
                list.Controls.Add(b);
            }

            var tip = new Panel { Dock = DockStyle.Bottom, Height = Px(110), BackColor = sidebar.BackColor };
            tip.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var shape = Draw.Round(new RectangleF(0, 0, tip.Width - 1, tip.Height - 1), Px(14))) using (var green = new SolidBrush(Theme.Green)) g.FillPath(green, shape);
                using (var f = Fonts.Get("mali", Px(15))) Ui.Text(g, "Mẹo nhỏ", f, Theme.Yellow, new Rectangle(Px(14), Px(10), tip.Width - Px(28), Px(24)), TextFormatFlags.Left);
                using (var f = Ui.Caption(k))
                    Ui.Text(g, "Nhấn " + Settings.ToggleKey.Display + " để bật hoặc tắt Phấn Trắng ở bất cứ đâu.", f, Color.FromArgb(217, 230, 223),
                        new Rectangle(Px(14), Px(38), tip.Width - Px(28), Px(66)), TextFormatFlags.WordBreak);
            };

            sidebar.Controls.Add(list);
            sidebar.Controls.Add(tip);
            sidebar.Controls.Add(brand);
            return sidebar;
        }

        public void ShowPage(string id)
        {
            var section = sections.FirstOrDefault(s => s.Id == id) ?? sections.Last();
            current = section.Id;
            foreach (var b in sectionButtons) { b.Selected = (string)b.Tag == current; b.Invalidate(); }
            body.SuspendLayout();
            foreach (var old in body.Controls.Cast<Control>().ToList()) old.Dispose();
            body.Controls.Clear();
            var flow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Location = new Point(Px(30), Px(24)), BackColor = Theme.Cream,
            };
            body.Controls.Add(flow);
            section.Fill(new PageBuilder(flow, k, ColumnWidth, section.Icon));
            body.ResumeLayout();
        }

        // ---- controls for rows ----

        ToggleSwitch Switch(bool value, Action<bool> set) { var s = new ToggleSwitch(value, k); s.Toggled += set; return s; }

        Control Pick(string[] labels, int selected, Action<int> set) { return new SelectBox(labels, selected, set, k); }

        /// <summary>A drop-down of numeric choices, matching the stored value to the nearest one.</summary>
        Control PickNumber(double[] values, string[] labels, double now, int fallback, Action<double> set)
        {
            int i = Array.FindIndex(values, v => Math.Abs(v - now) < 0.01);
            return Pick(labels, i < 0 ? fallback : i, j => set(values[j]));
        }

        Label KeyLabel(string text) { return new Label { Text = text, AutoSize = true, Font = Fonts.Get("uiSemi", 13.5f * k), ForeColor = Theme.Green, BackColor = Color.White }; }

        Control ShortcutField(KeyCombo now, Action<KeyCombo> save)
        {
            var box = new ShortcutBox(now, app) { Width = Px(200), Font = Ui.Body(k) };
            box.Changed += combo => { save(combo); app.ApplyShortcuts(); };
            return new FieldHost(box, k);
        }

        // ---- sections ----

        void General(PageBuilder p)
        {
            p.Title("Chung", null);
            if (Settings.Packaged)
            {
                // a Store copy starts with Windows through its startup task, which only Windows Settings can switch on
                var open = Button("Mở cài đặt Windows", false);
                open.Click += (s, e) =>
                {
                    try { Process.Start("ms-settings:startupapps"); }
                    catch (Exception ex) { Log.Write("mở cài đặt khởi động: " + ex.Message); }
                };
                p.Row("Mở cùng Windows", "Bật “Phấn Trắng” trong Cài đặt Windows › Ứng dụng › Khởi động.", open);
            }
            else p.Row("Mở cùng Windows", "Phấn Trắng chờ sẵn ở khay hệ thống mỗi khi bật máy.", Switch(Settings.LaunchAtLogin, v => Settings.LaunchAtLogin = v));
            p.Row("Luôn hiện thanh công cụ", "Tắt thì thanh chỉ hiện khi đưa chuột xuống đáy màn hình.", Switch(Settings.ToolbarAlways, v => Settings.ToolbarAlways = v));
            p.Row("Chữ dưới mỗi nút", "Tắt để thanh công cụ gọn hơn.", Switch(Settings.ToolbarLabels, v => Settings.ToolbarLabels = v));
            var barSizes = new[] { "s", "m", "l", "xl" };
            p.Row("Cỡ thanh công cụ", null, Pick(new[] { "Nhỏ", "Vừa", "Lớn", "Rất lớn" }, Array.IndexOf(barSizes, Settings.ToolbarSize), i => Settings.ToolbarSize = barSizes[i]));
            p.Row("Khi đang viết, vẽ", "Thanh công cụ mờ đi để không che nét, buông chuột là hiện lại.", PickNumber(new[] { 0.0, 0.15, 0.3, 0.55 },
                new[] { "Ẩn hẳn", "Rất mờ", "Mờ", "Hơi mờ" }, Settings.ToolbarDim, 2, v => Settings.ToolbarDim = v));
            var resetPlace = Button("Đặt lại vị trí", false);
            resetPlace.Click += (s, e) => Settings.ResetPlaces();
            p.Row("Vị trí thanh công cụ và khung gõ chữ", "Kéo thanh bằng 6 chấm ở đầu thanh; kéo khung gõ chữ ở chỗ trống, đổi cỡ ở góc dưới bên phải.", resetPlace);
            p.Row("Nút bật nhanh ở góc màn hình", "Khi Phấn Trắng đang tắt, một nút tròn nằm ở góc dưới bên phải. Bấm để bật, kéo để dời chỗ.", Switch(Settings.QuickButton, v => Settings.QuickButton = v));
            p.Row("Tiếng “cộc” khi chạm và đóng dấu", "Âm nhỏ như gõ thước lên bảng.", Switch(Settings.Sound, v => { Settings.Sound = v; if (v) app.Sounds.Tap(); }));
            p.Heading("Quyền riêng tư");
            p.Row("Gửi số liệu sử dụng ẩn danh", "Chỉ đếm số máy dùng Phấn Trắng và số lần dùng mỗi công cụ, để biết nên cải thiện gì. Không gửi tên, danh sách lớp, nội dung bài hay tên tệp. Không có mạng thì không gửi gì.", Switch(Settings.UsageStats, v => Settings.UsageStats = v));
            var policy = Button("Xem chính sách quyền riêng tư", false);
            policy.Click += (s, e) => { try { Process.Start("https://phantrang.org/quyen-rieng-tu.html"); } catch (Exception ex) { Log.Write("mở chính sách: " + ex.Message); } };
            p.Row("Chi tiết những gì được gửi", null, policy);
            p.Heading("Bấm thật vào slide");
            var keys = new[] { "alt", "ctrl", "shift", "off" };
            p.Row("Giữ phím này rồi bấm", "Dùng để chuyển slide, mở video như bình thường.", Pick(new[] { "Alt", "Ctrl", "Shift", "Tắt" }, Array.IndexOf(keys, Settings.InteractKey), i => Settings.InteractKey = keys[i]));
            p.Row("Khi đang giữ phím", null, Pick(new[] { "Hiện con trỏ chuột", "Vẫn giữ viên phấn" }, Settings.InteractKeepsPointer ? 1 : 0, i => Settings.InteractKeepsPointer = i == 1));
        }

        void PointerSection(PageBuilder p)
        {
            p.Title("Công cụ chỉ", "Hình dạng cả lớp nhìn theo trên màn chiếu.");
            p.Heading("Hình dạng");
            p.Add(new ChoiceGrid(ChoiceGrid.Look.Tiles, Catalog.Pointers, id => Settings.Pointer == id, id => Settings.Pointer = id, k, p.Width, 4, 112,
                (g, r, id) => Pointers.DrawIcon(g, id, r)));
            p.Heading("Cỡ");
            Func<string, double> size = id => double.Parse(id, CultureInfo.InvariantCulture);
            p.Add(new ChoiceGrid(ChoiceGrid.Look.Strip, Catalog.PointerSizes, id => Math.Abs(Settings.PointerSize - size(id)) < 0.005, id => Settings.PointerSize = size(id), k, p.Width));
            p.Heading("Hiển thị");
            p.Row("Đổ bóng dưới con trỏ", "Giúp viên phấn nổi lên trên slide nhiều chữ.", Switch(Settings.Shadow, v => Settings.Shadow = v));
        }

        void BoardSection(PageBuilder p)
        {
            p.Title("Bảng & chữ", "Màu mực, loại bảng khi bấm “Bảng”, và kiểu chữ khi gõ lên bảng.");
            p.Heading("Màu mực");
            p.Add(new ChoiceGrid(ChoiceGrid.Look.Dots, Catalog.Inks, id => Settings.InkChoice == id, id => Settings.InkChoice = id, k, p.Width));
            p.Note("Trên bảng xanh và bảng đen, mực tối tự đổi thành phấn trắng để cuối lớp vẫn đọc rõ.");
            p.Heading("Bảng mặc định");
            p.Add(new ChoiceGrid(ChoiceGrid.Look.Tiles, Catalog.Boards.Skip(1).ToArray(), id => Settings.BoardKind == id, id => Settings.BoardKind = id, k, p.Width, 3, 96,
                (g, r, id) =>
                {
                    using (var clip = Draw.Round(r, Px(8)))
                    {
                        var saved = g.Save();
                        g.SetClip(clip);
                        Paper.Paint(g, id, r, 90);
                        g.Restore(saved);
                    }
                }));
            p.Heading("Kiểu chữ khi gõ lên bảng");
            p.Note("Gõ bằng bàn phím, chữ hiện ra như viết tay. Có hàng ký hiệu toán: √ π ≤ ≥ ∠ △, hoặc gõ tắt ^2 → ², >= → ≥, sqrt → √.");
            p.Add(new ChoiceGrid(ChoiceGrid.Look.Tiles, Catalog.TextFonts, id => Settings.TextFont == id, id => Settings.TextFont = id, k, p.Width, 5, 92,
                (g, r, id) => { using (var f = Fonts.Get(id, Px(26))) Ui.Text(g, "Aă", f, Theme.Green, r, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine); }));
            p.Add(new ChoiceGrid(ChoiceGrid.Look.Strip, Catalog.TextSizes, id => Settings.TextSize == id, id => Settings.TextSize = id, k, p.Width));
            p.Row("Kẻ dòng khi gõ chữ", "Hiện các dòng kẻ mờ ngang bảng; bấm vào dòng nào thì chữ nằm ngay ngắn trên dòng đó.", Switch(Settings.TypeLines, v => Settings.TypeLines = v));
            p.Heading("Nét vẽ");
            p.Row("Nét trên slide", "Nét trên bảng luôn được giữ lại.", PickNumber(new[] { 0.0, 1, 3, 5, 10 },
                new[] { "Giữ đến khi xoá", "Mờ sau 1 giây", "Mờ sau 3 giây", "Mờ sau 5 giây", "Mờ sau 10 giây" }, Settings.FadeSeconds, 0, v => Settings.FadeSeconds = v));
            p.Row("Vẽ tạm", "Nét vẽ bằng công cụ “Vẽ tạm” tự biến mất, không cần lau.", PickNumber(new[] { 1.0, 2, 3, 5 },
                new[] { "Mất sau 1 giây", "Mất sau 2 giây", "Mất sau 3 giây", "Mất sau 5 giây" }, Settings.VanishSeconds, 1, v => Settings.VanishSeconds = v));
            var widthIds = Catalog.Widths.Select(w => w[0]).ToArray();
            p.Row("Độ dày nét", null, Pick(Catalog.Widths.Select(w => w[1]).ToArray(), Array.IndexOf(widthIds, Settings.PenWidth), i => Settings.PenWidth = widthIds[i]));
        }

        void GradeSection(PageBuilder p)
        {
            p.Title("Chấm bài", "Chọn những dấu hiện trên thanh công cụ ở chế độ Chấm bài. Bấm vào dấu để bật hoặc tắt.");
            Action<string> flip = id =>
            {
                var on = Settings.Stamps;
                if (!on.Contains(id)) on.Add(id);
                else if (on.Count > 1) on.Remove(id);   // at least one stamp stays
                Settings.Stamps = on;
            };
            p.Add(new ChoiceGrid(ChoiceGrid.Look.Tiles, Catalog.Stamps, id => Settings.Stamps.Contains(id), flip, k, p.Width, 3, 112,
                (g, r, id) => StampMark.DrawAt(g, id, new PointF(r.X + r.Width / 2f, r.Y + r.Height / 2f), id == "totlam" ? Math.Min(r.Height * 0.62f, r.Width * 0.36f) : r.Height * 0.95f, 1)).Several());
            p.Note("Dấu bật lên kèm tiếng “tách”. Cần giữ ít nhất một dấu.");
        }

        void KeysSection(PageBuilder p)
        {
            p.Title("Phím tắt", "Bấm vào ô rồi nhấn tổ hợp phím mới, có kèm Ctrl, Alt, Shift hoặc Win. Nhấn Backspace để bỏ phím tắt.");
            p.Row("Bật / tắt Phấn Trắng", null, ShortcutField(Settings.ToggleKey, c => Settings.ToggleKey = c));
            p.Row("Mở / đóng bảng", null, ShortcutField(Settings.BoardKey, c => Settings.BoardKey = c));
            p.Row("Xoá hết nét", null, ShortcutField(Settings.ClearKey, c => Settings.ClearKey = c));
            p.Heading("Khi Phấn Trắng đang bật");
            p.Row("Hoàn tác / Làm lại", null, KeyLabel("Ctrl + Z  ·  Ctrl + Y"));
            p.Row("Bấm thật vào slide", null, KeyLabel("Giữ " + (Settings.InteractName ?? "(đã tắt)")));
            p.Row("Tắt Phấn Trắng", null, KeyLabel("Esc"));
        }

        void LessonSection(PageBuilder p)
        {
            bool ppt = TeacherApp.PowerPointOn;
            p.Title("Bài giảng", ppt ? "Soạn các trang bảng rồi xuất thành PowerPoint để trình chiếu, hoặc PDF để in và gửi." : "Soạn các trang bảng rồi xuất thành PDF để in và gửi, hoặc thành ảnh.");
            var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(p.Width, 0), BackColor = Theme.Cream, Margin = new Padding(0, Px(6), 0, 0) };
            var list = new[]
            {
                Tuple.Create("Soạn bài mới", true, (Action)app.NewLesson),
                Tuple.Create("Thêm ảnh vào trang…", false, (Action)app.AddImage),
                Tuple.Create("Xuất PDF…", false, (Action)app.ExportPdf),
                Tuple.Create("Mở bài PowerPoint…", false, (Action)(() => app.OpenPptx())),
                Tuple.Create("Xuất PowerPoint…", false, (Action)(() => app.ExportPptx(false))),
                Tuple.Create("Thêm vào PowerPoint có sẵn…", false, (Action)(() => app.ExportPptx(true))),
                Tuple.Create("Xuất ảnh các trang…", false, (Action)app.ExportImages),
            };
            foreach (var a in list)
            {
                if (!ppt && a.Item1.Contains("PowerPoint")) continue;
                var b = Button(a.Item1, a.Item2);
                b.Margin = new Padding(0, 0, Px(8), Px(8));
                var act = a.Item3;
                b.Click += (s, e) => act();
                actions.Controls.Add(b);
            }
            p.Add(actions);
            p.Heading("Cách soạn");
            p.Note("1. Bấm “Soạn bài mới”: bảng mở ra, gõ chữ, vẽ hình, đóng dấu như lúc dạy.");
            p.Note("2. Bấm + trên thanh công cụ để thêm trang. Mỗi trang có thể dùng loại bảng riêng.");
            p.Note("3. Cần hình minh hoạ: “Thêm ảnh vào trang”, rồi bấm vào chỗ muốn đặt (Esc để thôi).");
            if (ppt)
            {
                p.Note("4. Trình chiếu: “Xuất PowerPoint” tạo bài mới, mỗi trang bảng là một trang chiếu. “Thêm vào PowerPoint có sẵn” đặt các trang vào cuối bài thầy cô đang có.");
                p.Note("5. In hoặc gửi: “Xuất PDF” gộp tất cả các trang vào một tệp PDF. Trên thanh công cụ: ⌄ cạnh nút Bảng → Bài giảng.");
                p.Note("Có sẵn bài PowerPoint? “Mở bài PowerPoint” biến mỗi trang chiếu thành một trang bảng để viết, chấm lên. Cần PowerPoint hoặc WPS trên máy; tệp gốc không bị sửa.");
            }
            else
            {
                p.Note("4. In hoặc gửi: “Xuất PDF” gộp tất cả các trang vào một tệp PDF; “Xuất ảnh” lưu mỗi trang thành một ảnh. Trên thanh công cụ: ⌄ cạnh nút Bảng → Bài giảng.");
            }
            p.Note("Các trang được xuất dưới dạng ảnh. Khi tắt Phấn Trắng hoặc soạn bài mới, các trang đang có được tự lưu: mở lại ở khay hệ thống › Bài giảng › Bản tự lưu.");
        }

        static string VersionText
        {
            get
            {
                var v = typeof(SettingsWindow).Assembly.GetName().Version;
                return v.Major + "." + v.Minor + (v.Build > 0 ? "." + v.Build : "");
            }
        }

        void About(PageBuilder p)
        {
            p.Title("Giới thiệu", null);
            var hero = new Panel { Width = p.Width, Height = Px(120), Margin = new Padding(0, 0, 0, Px(12)) };
            hero.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                using (var board = new SolidBrush(Theme.Board)) g.FillRectangle(board, 0, 0, hero.Width, hero.Height);
                using (var wood = new Pen(Theme.Frame, Px(8))) g.DrawRectangle(wood, Px(4), Px(4), hero.Width - Px(8), hero.Height - Px(8));
                Draw.Logo(g, new RectangleF(Px(24), Px(28), Px(64), Px(64)));
                using (var f = Fonts.Get("mali", Px(30))) using (var chalk = new SolidBrush(Theme.Chalk)) g.DrawString("Phấn Trắng", f, chalk, Px(104), Px(26));
                using (var f = Fonts.Get("ui", Px(13))) using (var yellow = new SolidBrush(Theme.Yellow)) g.DrawString("Bảng xanh trên mọi màn hình · phiên bản " + VersionText, f, yellow, Px(106), Px(74));
            };
            p.Add(hero);
            p.Note("Bộ công cụ giảng dạy cho thầy cô: chỉ bảng bằng viên phấn, gõ chữ như viết tay lên bảng xanh hay vở ô li, vẽ hình, chấm bài bằng Điểm 10 và Hoa điểm tốt.");
            p.Note("Phát triển bởi Viên Thế Hưng. Mã nguồn mở theo giấy phép MIT.");
            var mail = new LinkLabel { Text = "Góp ý, đề xuất chỉnh sửa: vienhung.work@gmail.com", AutoSize = true, Font = Ui.Body(k), Margin = new Padding(Px(4), Px(2), 0, Px(6)), LinkColor = Theme.Green, ActiveLinkColor = Theme.Green, LinkBehavior = LinkBehavior.HoverUnderline, UseMnemonic = false };
            mail.LinkArea = new LinkArea(mail.Text.IndexOf("vienhung"), "vienhung.work@gmail.com".Length);
            mail.LinkClicked += (s, e) =>
            {
                try { Process.Start("mailto:vienhung.work@gmail.com?subject=" + Uri.EscapeDataString("Góp ý Phấn Trắng " + VersionText)); }
                catch (Exception ex) { Log.Write("mở thư góp ý: " + ex.Message); }
            };
            p.Add(mail);
            p.Note("Phông chữ đi kèm (Be Vietnam Pro, Mali, Patrick Hand, Dancing Script, Charm) dùng giấy phép SIL Open Font License, xem thư mục assets\\fonts.");
            var folder = new LinkLabel { Text = "Mở thư mục cài đặt", AutoSize = true, Font = Ui.Body(k), Margin = new Padding(0, Px(8), 0, 0), LinkColor = Theme.Green, ActiveLinkColor = Theme.Green, LinkBehavior = LinkBehavior.HoverUnderline };
            folder.LinkClicked += (s, e) =>
            {
                try { Directory.CreateDirectory(Settings.Dir); Process.Start(Settings.Dir); }
                catch (Exception ex) { Log.Write("mở thư mục cài đặt: " + ex.Message); }
            };
            p.Add(folder);
            var guide = Button("Hướng dẫn sử dụng", true);
            guide.Margin = new Padding(0, Px(16), 0, 0);
            guide.Click += (s, e) => app.ShowGuide();
            p.Add(guide);
            var reset = Button("Đặt lại toàn bộ cài đặt…", false);
            reset.Margin = new Padding(0, Px(20), 0, 0);
            reset.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "Mọi lựa chọn sẽ trở về mặc định. Tuỳ chọn mở cùng Windows giữ nguyên.", "Đặt lại cài đặt?",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                Settings.ResetAll();
                app.ApplyShortcuts();
                ShowPage(current);
            };
            p.Add(reset);
        }
    }
}
