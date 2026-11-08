using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PhanTrang
{
    /// <summary>First run: a chalkboard greeting and three steps to start teaching.</summary>
    class WelcomeWindow : Form
    {
        readonly float k;
        public event Action StartTeaching, OpenGuide;

        int S(double v) { return (int)Math.Round(v * k); }

        public WelcomeWindow()
        {
            Text = "Chào mừng đến với Phấn Trắng";
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Paper;
            k = DeviceDpi / 96f;
            ClientSize = new Size(S(640), S(540));
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);

            Font = Ui.Body(k);
            // a Store copy cannot add itself to start-up (Windows Settings does that), so the box is not offered there
            var autostart = new CheckBox { Text = "Mở cùng Windows", Checked = true, AutoSize = true, Font = Ui.Body(k), ForeColor = Theme.Text, BackColor = Theme.Paper, UseMnemonic = false, Visible = !Settings.Packaged };
            var start = new RoundButton("Bắt đầu dạy", RoundButton.Kind.Primary, k) { BackColor = Theme.Paper };
            var settings = new RoundButton("Xem hướng dẫn", RoundButton.Kind.Secondary, k) { BackColor = Theme.Paper };
            int y = ClientSize.Height - S(32) - start.Height;
            start.Location = new Point(ClientSize.Width - S(44) - start.Width, y);
            settings.Location = new Point(start.Left - S(12) - settings.Width, y);
            autostart.Location = new Point(S(44), y + (start.Height - autostart.PreferredSize.Height) / 2);
            settings.Click += (s, e) => { Settings.LaunchAtLogin = autostart.Checked; Close(); if (OpenGuide != null) OpenGuide(); };
            start.Click += (s, e) => { Settings.LaunchAtLogin = autostart.Checked; Close(); if (StartTeaching != null) StartTeaching(); };
            Controls.Add(autostart);
            Controls.Add(settings);
            Controls.Add(start);
            AcceptButton = null;
            Shown += (s, e) => start.Focus();
            HandleCreated += (s, e) => Ui.PaperCaption(Handle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // chalkboard header
            var board = new Rectangle(0, 0, ClientSize.Width, S(170));
            using (var b = new SolidBrush(Theme.Board)) g.FillRectangle(b, board);
            using (var p = new Pen(Theme.Frame, S(8))) g.DrawRectangle(p, S(4), S(4), board.Width - S(8), board.Height - S(8));
            using (var f = Fonts.Get("mali", S(34))) Ui.Text(g, "Chào thầy cô!", f, Theme.Chalk, new Rectangle(S(44), S(38), S(440), S(50)), TextFormatFlags.Left);
            using (var f = Fonts.Get("mali", S(18))) Ui.Text(g, "Phấn Trắng đã sẵn sàng lên lớp.", f, Theme.Yellow, new Rectangle(S(44), S(92), S(440), S(30)), TextFormatFlags.Left);
            var sp = Pointers.Get("chalk", S(120));
            g.DrawImage(sp.Shadow, S(540) - sp.Tip.X + S(6), S(120) - sp.Tip.Y + S(12));
            g.DrawImage(sp.Img, S(540) - sp.Tip.X, S(120) - sp.Tip.Y);

            using (var f = Ui.Section(k)) Ui.Text(g, "3 BƯỚC ĐỂ BẮT ĐẦU", f, Theme.Green, new Rectangle(S(44), S(198), S(400), S(20)), TextFormatFlags.Left);

            string key = Settings.ToggleKey.IsNone ? "nút tròn ở góc màn hình" : Settings.ToggleKey.Display + ", hoặc bấm nút tròn ở góc màn hình";
            string[][] steps =
            {
                new[] { "Bật Phấn Trắng", "Nhấn " + key + "." },
                new[] { "Chọn chế độ ở thanh công cụ", "Chỉ để hướng dẫn cả lớp, Viết để gõ và vẽ lên bảng, Chấm bài để đóng dấu điểm. Bấm lại Vẽ tay để trở về chuột thường." },
                new[] { "Vẫn bấm được vào slide", "Chọn Chuột, hoặc giữ " + (Settings.InteractName ?? "Alt") + " rồi bấm để chuyển slide, mở video như bình thường." },
            };
            int y = S(232);
            using (var num = Fonts.Get("uiBold", S(14)))
            using (var tf = Ui.ItemTitle(k))
            using (var df = Fonts.Get("ui", S(13)))
                for (int i = 0; i < steps.Length; i++)
                {
                    var c = new Rectangle(S(44), y, S(30), S(30));
                    using (var b = new SolidBrush(Theme.GreenSoft)) g.FillEllipse(b, c);
                    Ui.Text(g, (i + 1).ToString(), num, Theme.Green, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    int tx = S(90), tw = ClientSize.Width - tx - S(44);
                    Ui.Text(g, steps[i][0], tf, Theme.Ink, new Rectangle(tx, y + S(4), tw, S(22)), TextFormatFlags.Left);
                    int dh = Ui.Measure(steps[i][1], df, tw, true).Height;
                    Ui.Text(g, steps[i][1], df, Theme.Dim, new Rectangle(tx, y + S(28), tw, dh), TextFormatFlags.WordBreak);
                    y += S(28) + dh + S(16);
                }
        }
    }
}
