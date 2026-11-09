using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace PhanTrang
{
    /// <summary>"Hướng dẫn sử dụng": six short topics, each a page of illustrated steps, with Trước / Tiếp to read in order.</summary>
    class GuideWindow : Form
    {
        readonly float k;
        readonly List<RoundButton> nav = new List<RoundButton>();
        readonly Panel body = new Panel();
        readonly GuideView view;
        readonly RoundButton prev, next;
        int index;
        public event Action OpenSettings;

        static readonly string[][] Topics =
        {
            new[] { "start", "Bắt đầu", "mode:point" },
            new[] { "modes", "Bốn chế độ", "mode:write" },
            new[] { "type", "Gõ chữ & toán", "type" },
            new[] { "board", "Bảng & trang", "board" },
            new[] { "lesson", "Bài giảng & xuất tệp", "newlesson" },
            new[] { "class", "Lớp học", "class" },
            new[] { "keys", "Phím tắt", "keyboard" },
        };

        int S(double v) { return (int)Math.Round(v * k); }

        public GuideWindow()
        {
            Text = "Phấn Trắng – Hướng dẫn sử dụng";
            AutoScaleMode = AutoScaleMode.None;
            k = DeviceDpi / 96f;
            Font = Ui.Body(k);
            BackColor = Theme.Cream;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(S(980), S(660));
            MinimumSize = new Size(S(820), S(540));
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            HandleCreated += (s, e) => Ui.PaperCaption(Handle);

            var side = new Panel { Dock = DockStyle.Left, Width = S(250), BackColor = Theme.Sidebar, Padding = new Padding(S(14), S(18), S(14), S(14)) };
            var brand = new Panel { Dock = DockStyle.Top, Height = S(78), BackColor = Theme.Sidebar };
            brand.Paint += (s, e) =>
            {
                var g = e.Graphics;
                Draw.Logo(g, new RectangleF(S(4), S(4), S(36), S(36)));
                using (var f = Fonts.Get("uiBold", S(18))) Ui.Text(g, "Hướng dẫn", f, Theme.Green, new Rectangle(S(48), S(2), brand.Width - S(48), S(24)), TextFormatFlags.Left);
                using (var f = Ui.Caption(k)) Ui.Text(g, "Đọc lần lượt, mỗi mục 1 phút", f, Theme.Dim, new Rectangle(S(48), S(26), brand.Width - S(48), S(20)), TextFormatFlags.Left);
            };
            var list = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Sidebar };
            for (int i = 0; i < Topics.Length; i++)
            {
                int idx = i;
                var b = new RoundButton((i + 1) + ".  " + Topics[i][1], RoundButton.Kind.Nav, k) { Width = S(222), Margin = new Padding(0, 0, 0, S(4)), Icon = Topics[i][2], BackColor = Theme.Sidebar };
                b.Click += (s, e) => ShowIndex(idx);
                nav.Add(b);
                list.Controls.Add(b);
            }
            side.Controls.Add(list);
            side.Controls.Add(brand);

            var foot = new Panel { Dock = DockStyle.Bottom, Height = S(76), BackColor = Theme.Cream, Padding = new Padding(S(40), S(14), S(40), S(18)) };
            prev = new RoundButton("‹  Trước", RoundButton.Kind.Secondary, k) { BackColor = Theme.Cream };
            next = new RoundButton("Tiếp", RoundButton.Kind.Primary, k) { BackColor = Theme.Cream };
            prev.Click += (s, e) => ShowIndex(index - 1);
            next.Click += (s, e) => { if (index < Topics.Length - 1) ShowIndex(index + 1); else Close(); };
            foot.Controls.Add(prev);
            foot.Controls.Add(next);
            foot.Resize += (s, e) => LayoutFoot(foot);
            foot.Paint += (s, e) => { using (var p = new Pen(Theme.Line)) e.Graphics.DrawLine(p, S(40), 0, foot.Width - S(40), 0); };

            body.Dock = DockStyle.Fill;
            body.AutoScroll = true;
            body.BackColor = Theme.Cream;
            view = new GuideView(k) { Location = new Point(S(40), S(28)) };
            view.Action += a => { if (a == "settings" && OpenSettings != null) OpenSettings(); };
            body.Controls.Add(view);
            body.Resize += (s, e) => FitView();

            Controls.Add(body);
            Controls.Add(foot);
            Controls.Add(side);
            ShowIndex(0);
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) Close();
                else if (e.KeyCode == Keys.PageDown || (e.KeyCode == Keys.Right && e.Alt)) ShowIndex(index + 1);
                else if (e.KeyCode == Keys.PageUp || (e.KeyCode == Keys.Left && e.Alt)) ShowIndex(index - 1);
            };
        }

        void LayoutFoot(Panel foot)
        {
            int y = S(14);
            next.Location = new Point(foot.Width - S(40) - next.Width, y);
            prev.Location = new Point(S(40), y);
        }

        void FitView()
        {
            int w = Math.Min(S(680), body.ClientSize.Width - S(80));
            view.SetWidth(Math.Max(S(420), w));
        }

        public void ShowTopic(string id) { int i = Array.FindIndex(Topics, t => t[0] == id); if (i >= 0) ShowIndex(i); }

        void ShowIndex(int i)
        {
            if (i < 0 || i >= Topics.Length) return;
            index = i;
            for (int n = 0; n < nav.Count; n++) { nav[n].Selected = n == i; nav[n].Invalidate(); }
            view.SetBlocks(Content(Topics[i][0]));
            body.AutoScrollPosition = Point.Empty;
            FitView();
            prev.Visible = i > 0;
            next.Text = i < Topics.Length - 1 ? "Tiếp: " + Topics[i + 1][1] + "  ›" : "Xong";
            next.Width = Ui.Measure(next.Text, next.Font).Width + S(44);
            if (next.Parent != null) LayoutFoot((Panel)next.Parent);
        }

        static string K(KeyCombo h, string fallback) { return h.IsNone ? fallback : h.Display.Replace(" + ", "+"); }

        static List<GuideView.Block> Content(string id)
        {
            var B = new List<GuideView.Block>();
            Action<string> h = t => B.Add(new GuideView.Block { Kind = "h", Text = t });
            Action<string> lead = t => B.Add(new GuideView.Block { Kind = "lead", Text = t });
            Action<string, string, string> row = (icon, t, d) => B.Add(new GuideView.Block { Kind = "row", Icon = icon, Text = t, Desc = d });
            Action<string[]> steps = s => B.Add(new GuideView.Block { Kind = "steps", Items = s });
            Action<string, string[][]> keys = (title, pairs) => B.Add(new GuideView.Block { Kind = "keys", Text = title, Pairs = pairs });
            Action<string> tip = t => B.Add(new GuideView.Block { Kind = "tip", Text = t });
            string toggle = K(Settings.ToggleKey, "nút tròn"), board = K(Settings.BoardKey, "nút Bảng"), clear = K(Settings.ClearKey, "nút Xoá");
            string alt = Settings.InteractName ?? "Alt";
            switch (id)
            {
                case "start":
                    h("Bắt đầu");
                    lead("Phấn Trắng phủ một lớp trong suốt lên màn hình đang chiếu. Thầy cô chỉ bài, viết, chấm bài ngay trên slide hoặc trên bảng xanh. Tắt đi là màn hình trở lại như cũ.");
                    row("power", "Bật và tắt", "Nhấn " + toggle + ", bấm nút tròn ở góc dưới màn hình, hoặc bấm biểu tượng Phấn Trắng ở khay hệ thống rồi chọn “Bắt đầu dạy”. Muốn thoát hẳn, bấm biểu tượng ở khay hệ thống rồi chọn “Thoát Phấn Trắng”.");
                    row("collapse", "Thanh công cụ ở đáy màn hình", "Mọi công cụ nằm ở đây. Giữ 6 chấm ở đầu thanh để kéo đi chỗ khác. Bấm ⌄ ở cuối thanh để thu gọn khi cần nhìn trọn slide.");
                    row("exit", "Tắt nhanh bằng Esc", "Nét vẽ trên slide sẽ được xoá. Các trang bảng vẫn được giữ lại.");
                    tip("Chưa nhớ nút nào làm gì? Rê chuột lên nút, tên nút hiện mờ trong chốc lát.");
                    break;
                case "modes":
                    h("Bốn chế độ");
                    lead("Bên trái thanh công cụ là 4 chế độ. Phần giữa thanh đổi theo chế độ đang chọn.");
                    row("mode:mouse", "Chuột", "Chuột Windows bình thường: chuyển slide, mở video. Nét đã vẽ vẫn nằm nguyên trên màn hình.");
                    row("mode:point", "Chỉ", "Viên phấn, thước gỗ, bút mực tím hoặc chấm laser. Bấm để chạm, cả lớp thấy một vòng sáng.");
                    row("mode:write", "Viết", "Gõ chữ, Vẽ tay, Vẽ tạm (tự biến mất), Dạ quang, Hình, Khăn lau. Bấm lại nút đang chọn để về Chuột.");
                    row("mode:grade", "Chấm bài", "Bấm vào bài làm để đóng dấu Điểm 10, Hoa điểm tốt, Tốt lắm!, Đúng, Sai, Ngôi sao.");
                    tip("Đang ở chế độ Chỉ mà cần bấm vào slide: giữ " + alt + " rồi bấm.");
                    break;
                case "type":
                    h("Gõ chữ & ký hiệu toán");
                    lead("Không cần viết bằng chuột: gõ bàn phím, chữ hiện lên như viết tay.");
                    steps(new[] { "Chọn Viết, rồi Gõ chữ.", "Bấm vào dòng kẻ mờ muốn viết, khung vàng cho biết chữ sẽ nằm ở đâu.", "Gõ trong khung “Gõ lên bảng”. Nhấn Enter để xuống dòng." });
                    row("open", "Khung gõ che chỗ viết?", "Kéo khung ở chỗ trống (chữ “Gõ lên bảng”) sang chỗ khác; kéo góc dưới bên phải để to hoặc nhỏ lại.");
                    row("type", "5 kiểu chữ", "Chữ phấn, Tròn tiểu học, Nét nghiêng, Bút lông, Chữ in rõ. Chọn cỡ chữ và màu phấn ngay trong khung gõ.");
                    keys("Gõ tắt ký hiệu toán", new[]
                    {
                        new[] { "^2", "²   bình phương" }, new[] { "sqrt", "√   căn bậc hai" }, new[] { ">=  <=", "≥  ≤" },
                        new[] { "!=", "≠   khác" }, new[] { "+-", "±   cộng trừ" }, new[] { "->  =>", "→  ⇒" }, new[] { "*", "×   nhân" }, new[] { "_1", "₁   chỉ số dưới" },
                    });
                    tip("Hàng “Ký hiệu toán” dưới khung gõ có thêm π ∞ ° ∠ △ ⊥ ∥ ∈ ⊂ ∪ ∩ α Δ ½. Bấm một lần là chèn vào.");
                    break;
                case "board":
                    h("Bảng & trang");
                    lead("Khi không có slide, mở bảng để viết như trên lớp.");
                    row("board", "Mở bảng", "Bấm nút Bảng hoặc nhấn " + board + ". Bấm ⌄ bên cạnh để đổi loại: xanh, đen, trắng, ô li, ô vuông, kẻ dòng.");
                    row("next", "Nhiều trang", "Bấm › để sang trang sau, + để thêm trang mới. Mỗi trang nhớ loại bảng riêng.");
                    row("undo", "Lỡ tay", "Ctrl+Z để hoàn tác, Ctrl+Y để làm lại, kể cả sau khi Xoá hết nét.");
                    tip("Trên bảng xanh và bảng đen, mực tối tự đổi thành phấn trắng để cuối lớp vẫn đọc rõ.");
                    break;
                case "lesson":
                    h("Bài giảng & xuất tệp");
                    lead("Soạn các trang bảng, rồi xuất ra PDF để in hoặc thành ảnh để gửi.");
                    steps(new[]
                    {
                        "Bấm ⌄ cạnh nút Bảng → Bài giảng → Bài mới.",
                        "Gõ chữ, vẽ, đóng dấu như lúc dạy. Bấm + để thêm trang.",
                        "Bài giảng → Xuất PDF hoặc Xuất ảnh.",
                    });
                    row("pdf", "Xuất PDF", "Cả bài vào một tệp PDF để in hoặc gửi.");
                    row("image", "Xuất ảnh", "Mỗi trang thành một ảnh PNG, tiện gửi cho học sinh ôn bài.");
                    tip("Các trang được xuất dưới dạng ảnh. Khi tắt Phấn Trắng, các trang được tự lưu: mở lại ở khay hệ thống → Bài giảng → Bài đang soạn dở.");
                    break;
                case "class":
                    h("Lớp học");
                    lead("Nút Lớp học trên thanh công cụ: đồ dùng cho cả lớp cùng nhìn lên màn chiếu.");
                    row("timer", "Đếm ngược", "Chọn 30 giây đến 15 phút, thêm bớt từng 10 giây hoặc 1 phút. Hết giờ có chuông.");
                    row("picker", "Gọi tên", "Bấm Quay để gọi ngẫu nhiên. Ai cũng được gọi một lần rồi mới quay lại từ đầu.");
                    row("groups", "Chia nhóm", "Chọn số nhóm rồi bấm Chia nhóm. Bấm “→ Thi đua” để các nhóm thành đội thi đua.");
                    row("contest", "Thi đua", "Bấm + hoặc − dưới mỗi tổ. Đội dẫn đầu đội vương miện.");
                    row("ruler", "Thước, ê-ke, đo góc", "Kéo để dời, kéo núm vàng để xoay (giữ Shift: mỗi nấc 15°). Vẽ bằng bút sát mép thước thành đường thẳng.");
                    row("compass", "Com-pa", "Kéo đầu phấn để vẽ cung tròn, kéo chân com-pa để mở rộng, kéo mũi kim để dời.");
                    tip("Danh sách lớp: gõ mỗi dòng một tên hoặc mở tệp .txt, .csv, Excel. Danh sách chỉ lưu trên máy này. Kéo góc dưới bên phải để phóng to thẻ, hoặc bấm nút phóng to trên đầu thẻ.");
                    break;
                default:
                    h("Phím tắt");
                    lead("Các phím dùng được ở bất cứ đâu, kể cả khi đang trình chiếu.");
                    keys(null, new[]
                    {
                        new[] { toggle, "Bật / tắt Phấn Trắng" }, new[] { board, "Mở / đóng bảng" }, new[] { clear, "Xoá hết nét" },
                        new[] { "Ctrl+Z", "Hoàn tác" }, new[] { "Ctrl+Y", "Làm lại" }, new[] { "Giữ " + alt, "Bấm thật vào slide" },
                        new[] { "Esc", "Tắt Phấn Trắng" }, new[] { "Enter", "Xuống dòng khi gõ chữ" },
                    });
                    B.Add(new GuideView.Block { Kind = "link", Text = "Đổi phím tắt trong Cài đặt", Desc = "settings" });
                    break;
            }
            return B;
        }
    }

    /// <summary>Lays out and paints one guide page; its height follows the content (the parent panel scrolls).</summary>
    class GuideView : Control
    {
        public class Block { public string Kind, Text, Desc, Icon; public string[] Items; public string[][] Pairs; public Rectangle Box; }

        readonly float k;
        List<Block> blocks = new List<Block>();
        Rectangle linkRect;
        bool linkHover;
        public event Action<string> Action;

        public GuideView(float k)
        {
            this.k = k;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Cream;
        }

        int S(double v) { return (int)Math.Round(v * k); }

        public void SetBlocks(List<Block> b) { blocks = b; Relayout(); }
        public void SetWidth(int w) { if (w != Width) { Width = w; Relayout(); } }

        int TextH(string s, Font f, int w) { return Ui.Measure(s, f, w, true).Height; }

        void Relayout()
        {
            int y = 0, W = Math.Max(100, Width);
            using (var title = Ui.Title(k)) using (var lead = Fonts.Get("ui", S(15))) using (var t = Ui.ItemTitle(k)) using (var d = Fonts.Get("ui", S(13.5)))
                foreach (var b in blocks)
                {
                    int h;
                    switch (b.Kind)
                    {
                        case "h": h = TextH(b.Text, title, W) + S(8); break;
                        case "lead": h = TextH(b.Text, lead, W) + S(20); break;
                        case "row": h = Math.Max(S(48), TextH(b.Text, t, W - S(90)) + S(4) + TextH(b.Desc, d, W - S(90))) + S(32); break;
                        case "steps":
                            h = S(16);
                            foreach (var s in b.Items) h += Math.Max(S(30), TextH(s, d, W - S(86))) + S(12);
                            h += S(4);
                            break;
                        case "keys": h = (b.Text != null ? S(34) : S(16)) + b.Pairs.Length * S(42) + S(10); break;
                        case "tip": h = TextH(b.Text, d, W - S(88)) + S(32); break;
                        default: h = S(40); break;
                    }
                    b.Box = new Rectangle(0, y, W, h);
                    y += h + S(b.Kind == "h" ? 4 : 12);
                }
            Height = Math.Max(S(100), y + S(8));
            Invalidate();
        }

        void Card(Graphics g, Rectangle r, Color fill, Color edge)
        {
            using (var p = Ui.Box(r, S(14), true))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, p);
                using (var pen = new Pen(edge, 1)) g.DrawPath(pen, p);
            }
        }

        void Kbd(Graphics g, string s, Font f, int x, int cy)
        {
            var size = Ui.Measure(s, f);
            var r = new Rectangle(x, cy - S(14), size.Width + S(18), S(28));
            using (var p = Ui.Box(r, S(7), true))
            {
                using (var b = new SolidBrush(Theme.Paper)) g.FillPath(b, p);
                using (var pen = new Pen(Theme.LineStrong, 1)) g.DrawPath(pen, p);
            }
            using (var pen = new Pen(Theme.LineStrong, S(2))) g.DrawLine(pen, r.Left + S(5), r.Bottom - 1, r.Right - S(5), r.Bottom - 1);
            Ui.Text(g, s, f, Theme.Ink, r, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            using (var title = Ui.Title(k)) using (var lead = Fonts.Get("ui", S(15))) using (var t = Ui.ItemTitle(k))
            using (var d = Fonts.Get("ui", S(13.5))) using (var bold = Fonts.Get("uiBold", S(14))) using (var mono = Fonts.Get("uiSemi", S(13.5)))
                foreach (var b in blocks)
                {
                    var r = b.Box;
                    switch (b.Kind)
                    {
                        case "h": Ui.Text(g, b.Text, title, Theme.Ink, r, TextFormatFlags.WordBreak); break;
                        case "lead": Ui.Text(g, b.Text, lead, Theme.Dim, new Rectangle(r.X, r.Y, r.Width, r.Height - S(20)), TextFormatFlags.WordBreak); break;
                        case "row":
                            {
                                Card(g, r, Color.White, Theme.Line);
                                var c = new Rectangle(r.X + S(18), r.Y + (r.Height - S(48)) / 2, S(48), S(48));
                                using (var br = new SolidBrush(Theme.GreenSoft)) g.FillEllipse(br, c);
                                int ib = 2 * S(12);
                                ToolIcons.Draw(g, b.Icon, new Rectangle(c.X + (c.Width - ib) / 2, c.Y + (c.Height - ib) / 2, ib, ib), b.Icon == "exit" ? Theme.Red : Theme.Green, false);
                                int tx = r.X + S(84), tw = r.Width - S(90) - S(18);
                                int th = TextH(b.Text, t, tw), dh = TextH(b.Desc, d, tw);
                                int y = r.Y + (r.Height - th - S(4) - dh) / 2;
                                Ui.Text(g, b.Text, t, Theme.Ink, new Rectangle(tx, y, tw, th), TextFormatFlags.WordBreak);
                                Ui.Text(g, b.Desc, d, Theme.Dim, new Rectangle(tx, y + th + S(4), tw, dh), TextFormatFlags.WordBreak);
                                break;
                            }
                        case "steps":
                            {
                                Card(g, r, Color.White, Theme.Line);
                                int y = r.Y + S(16);
                                for (int i = 0; i < b.Items.Length; i++)
                                {
                                    int dh = Math.Max(S(30), TextH(b.Items[i], d, r.Width - S(86)));
                                    var c = new Rectangle(r.X + S(20), y, S(30), S(30));
                                    using (var br = new SolidBrush(Theme.Green)) g.FillEllipse(br, c);
                                    Ui.Text(g, (i + 1).ToString(), bold, Theme.Chalk, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                                    Ui.Text(g, b.Items[i], d, Theme.Ink, new Rectangle(r.X + S(66), y + Math.Max(0, (S(30) - TextH(b.Items[i], d, r.Width - S(86))) / 2), r.Width - S(86), dh), TextFormatFlags.WordBreak);
                                    y += dh + S(12);
                                }
                                break;
                            }
                        case "keys":
                            {
                                Card(g, r, Color.White, Theme.Line);
                                int y = r.Y + S(16);
                                if (b.Text != null)
                                {
                                    using (var sec = Ui.Section(k)) Ui.Text(g, b.Text.ToUpperInvariant(), sec, Theme.Green, new Rectangle(r.X + S(20), y, r.Width - S(40), S(20)), TextFormatFlags.Left);
                                    y += S(18);
                                }
                                for (int i = 0; i < b.Pairs.Length; i++)
                                {
                                    int cy = y + S(21);
                                    Kbd(g, b.Pairs[i][0], mono, r.X + S(20), cy);
                                    Ui.Text(g, b.Pairs[i][1], d, Theme.Text, new Rectangle(r.X + S(200), cy - S(12), r.Width - S(220), S(24)), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                                    if (i < b.Pairs.Length - 1) using (var pen = new Pen(Theme.Line)) g.DrawLine(pen, r.X + S(20), y + S(42), r.Right - S(20), y + S(42));
                                    y += S(42);
                                }
                                break;
                            }
                        case "tip":
                            {
                                Card(g, r, Theme.GreenSoft, Color.FromArgb(196, 222, 207));
                                var c = new Rectangle(r.X + S(18), r.Y + S(16), S(28), S(28));
                                using (var br = new SolidBrush(Theme.Green)) g.FillEllipse(br, c);
                                Ui.Text(g, "!", bold, Theme.Yellow, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                                Ui.Text(g, b.Text, d, Theme.Green, new Rectangle(r.X + S(60), r.Y + S(16), r.Width - S(80), r.Height - S(28)), TextFormatFlags.WordBreak);
                                break;
                            }
                        case "link":
                            {
                                var size = Ui.Measure(b.Text + "  ›", t);
                                linkRect = new Rectangle(r.X, r.Y + S(8), size.Width, size.Height);
                                using (var f = new Font(t, linkHover ? FontStyle.Underline : FontStyle.Regular))
                                    Ui.Text(g, b.Text + "  ›", f, Theme.Green, linkRect, TextFormatFlags.Left);
                                break;
                            }
                    }
                }
        }

        Block LinkBlock { get { return blocks.FirstOrDefault(b => b.Kind == "link"); } }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool h = LinkBlock != null && linkRect.Contains(e.Location);
            Cursor = h ? Cursors.Hand : Cursors.Default;
            if (h != linkHover) { linkHover = h; Invalidate(); }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            var l = LinkBlock;
            if (l != null && linkRect.Contains(e.Location) && Action != null) Action(l.Desc);
        }
    }
}
