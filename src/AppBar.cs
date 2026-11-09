using System;
using System.Collections.Generic;
using System.Linq;

namespace PhanTrang
{
    /// <summary>The toolbar's buttons and what they do.</summary>
    sealed partial class TeacherApp
    {
        /// <summary>The tools of the "Viết" mode; the last one used is picked up again when the mode is chosen.</summary>
        static readonly string[] WritingTools = { "type", "draw", "vanish", "hl", "shape", "eraser", "image" };

        /// <summary>Four modes: Chuột (normal mouse, ink stays), Chỉ, Viết, Chấm bài.</summary>
        static string ModeOf(string tool)
        {
            switch (tool)
            {
                case "mouse": return "mouse";
                case "pointer": return "point";
                case "stamp": return "grade";
                default: return "write";
            }
        }

        static bool IsPen(string tool) { return tool == "draw" || tool == "vanish" || tool == "hl"; }

        static ToolButton Btn(string id, string icon, string label, bool on = false) { return new ToolButton { Id = id, Icon = icon, Label = label, On = on }; }

        static ToolButton Small(string id, string icon, string label, string tip, bool off = false)
        {
            return new ToolButton { Id = id, Icon = icon, Label = label, Tip = tip, IconOnly = true, Off = off };
        }

        static string WithKey(KeyCombo k) { return k.IsNone ? "" : " · " + k.Display.Replace(" + ", "+"); }

        string InkHex { get { var ink = Catalog.Inks.FirstOrDefault(c => c[0] == Settings.InkChoice) ?? Catalog.Inks[0]; return ink[2]; } }

        List<ToolButton> MainButtons()
        {
            string tool = Settings.Tool, mode = ModeOf(tool);
            var row = new List<ToolButton>();
            foreach (var m in new[]
            {
                new[] { "mouse", "Chuột", "Chuột thường: bấm vào slide, nét vẽ vẫn giữ" },
                new[] { "point", "Chỉ", "Chỉ bảng bằng viên phấn" },
                new[] { "write", "Viết", "Gõ chữ, vẽ, tô dạ quang, vẽ hình" },
                new[] { "grade", "Chấm bài", "Đóng dấu Điểm 10, Hoa điểm tốt…" },
            })
                row.Add(new ToolButton { Id = "mode:" + m[0], Icon = "mode:" + m[0], Label = m[1], Tip = mode == m[0] ? null : m[2], Inline = true, Group = "modes", On = mode == m[0] });

            var power = Small("exit", "power", "Tắt Phấn Trắng", "Tắt Phấn Trắng · Esc");
            row.Add(ToolButton.Line());
            if (collapsed)
            {
                row.Add(Small("expand", "expand", "Mở rộng", "Hiện đủ thanh công cụ"));
                row.Add(power);
                return row;
            }

            if (mode == "point")
                foreach (var p in Catalog.Pointers) { var b = Btn("look:" + p[0], "pointer:" + p[0], p[1], p[0] == Settings.Pointer); b.Width = 76; row.Add(b); }
            else if (mode == "grade")
            {
                var shown = Settings.Stamps;
                foreach (var s in Catalog.Stamps.Where(s => shown.Contains(s[0])))
                {
                    var b = Btn("stamp:" + s[0], "stamp:" + s[0], s[1], s[0] == Settings.StampKind);
                    b.Width = s[0] == "totlam" || s[0] == "hoa" ? 96 : 78;
                    row.Add(b);
                }
            }
            else
            {
                // the "Viết" tools (also shown, none selected, in Chuột mode: one click goes back to drawing)
                row.Add(Btn("tool:type", "type", "Gõ chữ", tool == "type"));
                row.Add(Btn("tool:draw", "draw", "Vẽ tay", tool == "draw"));
                row.Add(Btn("tool:vanish", "vanish", "Vẽ tạm", tool == "vanish"));
                row.Add(Btn("tool:hl", "hl", "Dạ quang", tool == "hl"));
                var shapes = Btn("tool:shape", "shape:" + Settings.ShapeKind, "Hình", tool == "shape"); shapes.Split = true; row.Add(shapes);
                row.Add(Btn("tool:eraser", "eraser", "Khăn lau", tool == "eraser"));
                if (Stage.Book.OnBoard)   // pictures belong on the board, never on top of a slide
                    row.Add(new ToolButton { Id = "tool:image", Icon = "image", Label = "Ảnh", On = tool == "image", Split = true, Tip = "Chọn, kéo, xoay ảnh trên bảng" });
            }

            if (mode != "point")
            {
                row.Add(ToolButton.Line());
                row.Add(new ToolButton { Id = "color", Icon = "color:" + InkHex, Label = "Màu", Split = true, OpensMenu = true });
            }

            row.Add(ToolButton.Line());
            row.Add(Small("undo", "undo", "Hoàn tác", "Hoàn tác · Ctrl+Z", !Stage.CanUndo));
            row.Add(Small("redo", "redo", "Làm lại", "Làm lại · Ctrl+Y", !Stage.CanRedo));
            row.Add(Small("clear", "clear", "Xoá bảng", "Xoá hết nét (Hoàn tác để lấy lại)" + WithKey(Settings.ClearKey), !Stage.HasInk));

            row.Add(ToolButton.Line());
            row.Add(new ToolButton { Id = "class", Icon = "class", Label = "Lớp học", OpensMenu = true, Split = true, Width = 74, Tip = "Đếm ngược, gọi tên, chia nhóm, thi đua, thước kẻ, com-pa…" });

            row.Add(ToolButton.Line());
            bool board = Stage.Book.OnBoard;
            row.Add(new ToolButton
            {
                Id = "board", Icon = "board:" + Stage.Board, Label = board ? Catalog.Label(Catalog.Boards, Stage.Board) : "Bảng",
                Inline = true, Split = true, Group = "board", Tip = (board ? "Tắt bảng" : "Mở bảng") + WithKey(Settings.BoardKey),
            });
            if (board)
            {
                var book = Stage.Book;
                row.Add(new ToolButton { Id = "prev", Icon = "prev", Label = "Trang trước", Tip = "Trang trước", IconOnly = true, Group = "board", Off = book.OnFirst, Width = 36 });
                row.Add(new ToolButton { Id = "page", Icon = "page", Label = book.PageLabel, Group = "board", Width = 52 });
                row.Add(book.OnLast
                    ? new ToolButton { Id = "next", Icon = "plus", Label = "Trang mới", Tip = Stage.HasInk ? "Thêm trang mới" : "Viết lên trang này rồi mới thêm trang", IconOnly = true, Group = "board", Off = !Stage.HasInk, Width = 36 }
                    : new ToolButton { Id = "next", Icon = "next", Label = "Trang sau", Tip = "Trang sau", IconOnly = true, Group = "board", Width = 36 });
            }

            row.Add(ToolButton.Line());
            row.Add(Small("collapse", "collapse", "Thu gọn", "Thu gọn thanh công cụ"));
            row.Add(power);
            return row;
        }

        static string BoardShortName(string id, string fallback)
        {
            switch (id)
            {
                case "green": return "Xanh";
                case "black": return "Đen";
                case "white": return "Trắng";
                case "oli": return "Ô li";
                case "grid": return "Ô vuông";
                case "lines": return "Kẻ dòng";
                default: return fallback;
            }
        }

        static ToolButton Sized(ToolButton b, int width) { b.Width = width; return b; }

        List<ToolButton> ChoiceButtons(string id)
        {
            switch (id)
            {
                case "tool:shape":
                    return Catalog.Shapes.Select(s => Sized(Btn("shape:" + s[0], "shape:" + s[0], s[1], s[0] == Settings.ShapeKind), 92)).ToList();
                case "color":
                    var inks = Catalog.Inks.Select(c => Sized(Btn("color:" + c[0], "color:" + c[2], c[1], c[0] == Settings.InkChoice), 74)).ToList();
                    inks.Add(ToolButton.Line());
                    inks.AddRange(Catalog.Widths.Select(w => Sized(Btn("width:" + w[0], "width:" + w[0], w[1], w[0] == Settings.PenWidth), 70)));
                    return inks;
                case "board":
                    // the six boards; switching the board off is the chip itself, lessons have their own small row
                    var boards = Catalog.Boards.Skip(1).Select(b => Sized(Btn("board:" + b[0], "swatch:" + b[0], BoardShortName(b[0], b[1]), b[0] == Stage.Board), 72)).ToList();
                    boards.Add(ToolButton.Line());
                    boards.Add(new ToolButton { Id = "lesson", Icon = "newlesson", Label = "Bài giảng", Split = true, OpensMenu = true, Width = 96 });
                    return boards;
                case "tool:image":
                    bool chosen = Stage.Pictures.Chosen != null;
                    return new List<ToolButton>
                    {
                        new ToolButton { Id = "image:add", Icon = "addimage", Label = "Thêm ảnh", Width = 80 },
                        new ToolButton { Id = "image:left", Icon = "rotl", Label = "Xoay trái", Width = 80, Off = !chosen },
                        new ToolButton { Id = "image:right", Icon = "rotr", Label = "Xoay phải", Width = 80, Off = !chosen },
                        new ToolButton { Id = "image:delete", Icon = "clear", Label = "Xoá ảnh", Width = 76, Off = !chosen },
                    };
                case "class": return ClassChoices();
                case "lesson":
                    bool any = Stage.Book.HasLesson;
                    return new List<ToolButton>
                    {
                        new ToolButton { Id = "lesson:new", Icon = "newlesson", Label = "Bài mới", Width = 76 },
                        new ToolButton { Id = "image:add", Icon = "addimage", Label = "Thêm ảnh", Width = 80 },
                        new ToolButton { Id = "lesson:pdf", Icon = "pdf", Label = "Xuất PDF", Width = 76, Off = !any, Tip = "Cả bài giảng thành một tệp PDF để in hoặc gửi" },
                        new ToolButton { Id = "lesson:images", Icon = "image", Label = "Xuất ảnh", Width = 76, Off = !any },
                    };
            }
            return new List<ToolButton>();
        }

        void SetTool(string tool)
        {
            if (tool != Settings.Tool) Usage.Count("tool." + tool);
            if (WritingTools.Contains(tool)) Settings.WriteTool = tool;
            Settings.Tool = tool;
            if (tool == "type") OpenTyping(); else CloseTyping();
            UpdateInput();
        }

        void OnToolbar(string id)
        {
            int colon = id.IndexOf(':');
            string kind = colon < 0 ? id : id.Substring(0, colon), arg = colon < 0 ? "" : id.Substring(colon + 1);
            if (kind == "more") { toolbar.ToggleChoices(arg); return; }
            string before = Settings.Tool;
            Do(kind, arg, id);
            if (Settings.Tool != before) Coach(Settings.Tool);
        }

        /// <summary>A one-line tip above the bar after switching tools, the first three times for each tool.</summary>
        void Coach(string tool)
        {
            string[][] tip;
            switch (tool)
            {
                case "mouse": tip = new[] { new[] { "Chuột thường:", "bấm vào slide như bình thường" }, new[] { "Viết hoặc Chỉ", "để dùng lại phấn" } }; break;
                case "pointer": tip = new[] { new[] { "Bấm", "để chạm cho cả lớp nhìn" }, new[] { "Giữ " + (Settings.InteractName ?? "Alt"), "để bấm vào slide" } }; break;
                case "type": tip = new[] { new[] { "Bấm", "vào chỗ muốn viết rồi gõ" }, new[] { "Enter", "xuống dòng" } }; break;
                case "draw":
                case "hl": tip = new[] { new[] { "Kéo", "để vẽ" }, new[] { "Bấm lại nút này", "để về chuột thường" } }; break;
                case "vanish": tip = new[] { new[] { "Kéo", "để vẽ, nét tự mất sau " + Settings.VanishSeconds + " giây" }, new[] { "Bấm lại nút này", "để về chuột thường" } }; break;
                case "shape": tip = new[] { new[] { "Kéo", "để vẽ hình" }, new[] { "Giữ Shift", "để vẽ thẳng hoặc vẽ đều" } }; break;
                case "eraser": tip = new[] { new[] { "Kéo", "qua nét cần lau" }, new[] { "Ctrl+Z", "để lấy lại" } }; break;
                case "image": tip = new[] { new[] { "Kéo ảnh", "để di chuyển" }, new[] { "Kéo nút tròn", "để xoay" }, new[] { "Kéo góc", "để phóng to" } }; break;
                case "stamp": tip = new[] { new[] { "Bấm", "vào bài để đóng dấu" }, new[] { "Chọn dấu", "ở thanh bên dưới" } }; break;
                default: return;
            }
            if (Settings.HintSeen(tool) >= 3) return;
            Settings.MarkHint(tool);
            toolbar.Coach(tip);
        }

        void Do(string kind, string arg, string id)
        {
            switch (kind)
            {
                case "mode":
                    toolbar.CloseChoices();
                    if (arg == "mouse") SetTool("mouse");
                    else if (arg == "point") SetTool("pointer");
                    else if (arg == "grade") SetTool("stamp");
                    else
                    {
                        string w = WritingTools.Contains(Settings.WriteTool) ? Settings.WriteTool : "type";
                        if (w == "image" && !Stage.Book.OnBoard) w = "type";
                        SetTool(w);
                    }
                    return;
                case "tool":
                    toolbar.CloseChoices();
                    // clicking the pen already in hand puts it down: back to the normal mouse, the ink stays
                    SetTool(IsPen(arg) && Settings.Tool == arg ? "mouse" : arg);
                    if (arg == "image" && !Stage.Current.Marks.OfType<PictureMark>().Any()) AddImage();   // nothing to arrange yet: pick one
                    return;
                case "look": Settings.Pointer = arg; SetTool("pointer"); return;
                case "shape": Settings.ShapeKind = arg; SetTool("shape"); return;
                case "stamp": Settings.StampKind = arg; SetTool("stamp"); return;
                case "color":
                    Settings.InkChoice = arg;
                    if (Settings.Tool == "mouse" || Settings.Tool == "pointer") SetTool(Settings.WriteTool);
                    return;
                case "width": Settings.PenWidth = arg; return;
                case "board": if (arg.Length > 0) SetBoard(arg); else ToggleBoard(); return;
                case "image":
                    if (arg == "add") AddImage();
                    else
                    {
                        if (arg == "left") Stage.Pictures.Turn(-90);
                        else if (arg == "right") Stage.Pictures.Turn(90);
                        else if (arg == "delete") Stage.Pictures.Delete();
                        Refresh();
                    }
                    return;
                case "class": toolbar.CloseChoices(); ClassTool(arg); return;
                case "lesson":
                    if (arg == "new") NewLesson();
                    else if (arg == "pdf") ExportPdf();
                    else if (arg == "images") ExportImages();
                    return;
            }
            switch (id)
            {
                case "undo": Stage.Undo(); Refresh(); break;
                case "redo": Stage.Redo(); Refresh(); break;
                case "clear": ClearInk(); break;
                case "prev": Stage.PrevPage(); RefreshTyping(); Refresh(); break;
                case "next": Stage.NextPage(); RefreshTyping(); Refresh(); break;
                case "collapse": collapsed = true; CloseTyping(); Refresh(); break;
                case "expand": collapsed = false; if (Settings.Tool == "type") OpenTyping(); Refresh(); break;
                case "exit": TurnOff(); break;
            }
        }

        void SetBoard(string kind)
        {
            if (kind != "none" && kind != Stage.Board) Usage.Count("board." + kind);
            if (kind == "none" && Settings.Tool == "image") SetTool("pointer");
            if (kind != "none") Settings.BoardKind = kind;
            Stage.SetBoard(kind);
            RefreshTyping();
            Refresh();
        }

        void ToggleBoard()
        {
            if (!on) TurnOn();
            if (!on) return;
            SetBoard(Stage.Book.OnBoard ? "none" : (Stage.Book.PageBoard ?? Settings.BoardKind));
        }
    }
}
