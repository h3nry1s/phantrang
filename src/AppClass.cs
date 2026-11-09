using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PhanTrang
{
    /// <summary>The "Lớp học" tools: countdown, calling names, groups, team points and the geometry instruments.</summary>
    sealed partial class TeacherApp
    {
        /// <summary>Our floating windows: the Windows pointer comes back over them, clicks reach them, and they stay above the board.</summary>
        IEnumerable<Form> Floating
        {
            get
            {
                foreach (var f in new Form[] { settingsWin, typePanel, welcome }) if (f != null) yield return f;
                foreach (var c in cards.ToList()) yield return c;
            }
        }

        static readonly string[][] ClassTools =
        {
            new[] { "timer", "Đếm ngược", "Đồng hồ đếm ngược, hết giờ có chuông" },
            new[] { "picker", "Gọi tên", "Gọi tên ngẫu nhiên từ danh sách lớp" },
            new[] { "groups", "Chia nhóm", "Chia lớp thành nhóm ngẫu nhiên" },
            new[] { "contest", "Thi đua", "Bảng điểm thi đua giữa các tổ, nhóm" },
        };

        static readonly string[][] Instruments =
        {
            new[] { "ruler", "ruler", "Thước kẻ", "Kéo để dời, núm vàng để xoay. Vẽ sát mép thước thành đường thẳng" },
            new[] { "protractor", "protractor", "Đo góc", "Thước đo góc: núm vàng để xoay" },
            new[] { "set45", "setsquare", "Ê-ke 45°", "Ê-ke vuông cân" },
            new[] { "set30", "setsquare", "Ê-ke 30°", "Ê-ke 30° – 60°" },
            new[] { "compass", "compass", "Com-pa", "Kéo đầu phấn để vẽ cung tròn, kéo chân để mở rộng" },
        };

        List<ToolButton> ClassChoices()
        {
            var list = ClassTools.Select(t => new ToolButton { Id = "class:" + t[0], Icon = t[0], Label = t[1], Tip = t[2], Width = 84, On = cards.Any(c => c.Tag as string == t[0]) }).ToList();
            list.Add(ToolButton.Line());
            list.AddRange(Instruments.Select(t => new ToolButton { Id = "class:" + t[0], Icon = t[1], Label = t[2], Tip = t[3], Width = 80, On = geo.Has(t[0]) }));
            return list;
        }

        /// <summary>A "Lớp học" choice: open (or bring forward) a card, or put an instrument out / away.</summary>
        public void ClassTool(string id)
        {
            Usage.Count("class." + id);
            if (Instruments.Any(t => t[0] == id))
            {
                if (!on) TurnOn();
                if (!on) return;
                geo.Toggle(id, work, layer.Monitor.Height > 0 ? layer.Monitor.Height : 1080);
                Restack();
                return;
            }
            var open = cards.FirstOrDefault(c => c.Tag as string == id);
            if (open != null) { open.BringToFront(); Win32.SetWindowPos(open.Handle, Win32.Topmost, 0, 0, 0, 0, Win32.Swp.KeepPlace); return; }
            ClassCard card;
            double k = CardScale;
            switch (id)
            {
                case "timer": card = new TimerCard(k, Sounds); break;
                case "picker": card = new PickerCard(k, Sounds, EditRoster); break;
                case "groups": card = new GroupsCard(k, EditRoster, StartContest); break;
                case "contest": card = new ContestCard(k, Sounds, null); break;
                default: return;
            }
            ShowCard(card, id);
        }

        double CardScale
        {
            get
            {
                var s = on ? screenScale : Win32.ScreenAtCursor().Scale;
                return Math.Max(1, s);
            }
        }

        void ShowCard(ClassCard card, string id)
        {
            card.Tag = id;
            var area = on ? work : Win32.ScreenAtCursor().Work;
            // a little cascade so two cards do not sit exactly on each other
            int shift = cards.Count * (int)(28 * CardScale);
            card.Location = new Point(
                Math.Max(area.Left, Math.Min(area.Right - card.Width, area.Left + (area.Width - card.Width) / 2 + shift)),
                Math.Max(area.Top, Math.Min(area.Bottom - card.Height, area.Top + (area.Height - card.Height) / 3 + shift)));
            card.Closing2 += c => { cards.Remove(c); toolbar.Rebuild(); };
            cards.Add(card);
            card.Show();
            toolbar.Rebuild();
            if (on) { Restack(); Frame(); }
        }

        /// <summary>Groups become the teams of "Thi đua".</summary>
        void StartContest(IList<string> teams)
        {
            var open = cards.OfType<ContestCard>().FirstOrDefault();
            if (open != null) { open.UseTeams(teams); open.BringToFront(); return; }
            ShowCard(new ContestCard(CardScale, Sounds, teams), "contest");
        }

        /// <summary>The class list editor. True if the teacher saved a list.</summary>
        bool EditRoster()
        {
            bool saved = OverBoard(o =>
            {
                using (var w = new RosterWindow((float)CardScale)) { w.TopMost = true; return w.ShowDialog(o) == DialogResult.OK; }
            });
            if (saved) foreach (var p in cards.OfType<PickerCard>()) p.Reload();
            return saved;
        }

        ToolStripMenuItem ClassMenu()
        {
            var item = Entry("Lớp học", null, "class");
            item.DropDown.Renderer = menu.Renderer;
            item.DropDown.Font = menu.Font;
            foreach (var t in ClassTools) { string id = t[0]; item.DropDownItems.Add(Entry(t[1], () => ClassTool(id), id)); }
            item.DropDownItems.Add(new ToolStripSeparator());
            foreach (var t in Instruments) { string id = t[0]; item.DropDownItems.Add(Entry(t[2], () => ClassTool(id), t[1])); }
            item.DropDownItems.Add(new ToolStripSeparator());
            item.DropDownItems.Add(Entry("Danh sách lớp…", () => EditRoster(), "picker"));
            return item;
        }
    }
}
