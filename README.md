# Phấn Trắng

Bảng xanh trên mọi màn hình: công cụ giảng dạy miễn phí cho thầy cô trên Windows 10 và 11. Chỉ bài bằng viên phấn, viết như tay, vẽ, chấm bài bằng Điểm 10 ngay trên slide, PDF hay trang web đang chiếu.

Giới thiệu và thử từng tính năng: **https://phantrang.org**

## Tải về và cài đặt

**[Phấn Trắng trên Microsoft Store](https://apps.microsoft.com/detail/9N4CWVRK45FF?hl=vi-VN&gl=VN)**. Mở trang, bấm cài đặt. Bản Store được Microsoft kiểm duyệt nên Windows không cảnh báo, Smart App Control không chặn, và ứng dụng tự cập nhật khi có bản mới. Gỡ cài đặt: **Cài đặt Windows › Ứng dụng › Phấn Trắng › Gỡ cài đặt**.

## Dùng nhanh

| Phím | Tác dụng |
|---|---|
| `Ctrl + Alt + P` | Bật / tắt Phấn Trắng |
| `Ctrl + Alt + B` | Mở / đóng bảng |
| `Ctrl + Alt + X` | Xoá hết nét |
| `Ctrl + Z` / `Ctrl + Y` | Hoàn tác / làm lại (khi đang bật) |
| Giữ `Alt` + bấm | Bấm thật vào slide, video, trang web |
| `Esc` | Tắt Phấn Trắng |

Thanh công cụ ở đáy màn hình (giữ 6 chấm ở đầu thanh để kéo đi chỗ khác) có 4 chế độ: **Chuột**, **Chỉ**, **Viết**, **Chấm bài**. Menu **Bảng ⌄ › Bài giảng** để soạn các trang bảng rồi xuất thành **PDF** hay ảnh PNG. Nút **Lớp học** có đồng hồ đếm ngược, gọi tên ngẫu nhiên (danh sách gõ tay hoặc mở từ .txt, .csv, Excel), chia nhóm, bảng thi đua, cùng thước kẻ, thước đo góc, ê-ke và com-pa vẽ thẳng lên màn hình. Hướng dẫn đầy đủ trong ứng dụng: chuột phải biểu tượng viên phấn ở khay hệ thống › **Hướng dẫn sử dụng**.

Phấn Trắng không thu thập dữ liệu cá nhân. Khi có mạng, ứng dụng chỉ gửi số liệu sử dụng ẩn danh (số máy, số lần dùng mỗi công cụ) để cải thiện sản phẩm; tắt được trong Cài đặt › Chung ([chính sách quyền riêng tư](https://phantrang.org/quyen-rieng-tu.html)). Nếu ứng dụng bị tắt đột ngột khi đang bật và con trỏ chuột biến mất, mở lại Phấn Trắng là con trỏ hiện lại.

## Tự build từ mã nguồn

Không cần cài thêm gì: Windows 10 và 11 đã có sẵn trình biên dịch C#.

1. Trên trang GitHub này, bấm nút xanh **Code** › **Download ZIP**.
2. Bấm chuột phải tệp ZIP › **Extract All…** (Giải nén tất cả).
3. Trong thư mục vừa giải nén, nhấn đúp **`build.cmd`**. Nếu Windows hỏi, chọn **Thông tin thêm** › **Vẫn chạy**.
4. Khoảng nửa phút sau, thư mục `dist` mở ra: nhấn đúp **`PhanTrang.exe`** để chạy (giữ thư mục `assets` nằm cạnh).

Bản tự build không có chữ ký số, nên máy bật **Smart App Control** có thể không cho chạy; khi đó dùng bản trên Microsoft Store.

## Dành cho người phát triển

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1               # dist\PhanTrang.exe
powershell -ExecutionPolicy Bypass -File tests\run-tests.ps1     # build + kiểm thử tự động
```

Chỉ cần Windows: script dùng `csc.exe` (.NET Framework 4.x) có sẵn, không cần Visual Studio. GitHub Actions tự build và kiểm thử mỗi lần đẩy mã.

| Thư mục | Nội dung |
|---|---|
| `src/` | Mã nguồn ứng dụng (C#, WinForms) |
| `assets/fonts/` | Phông chữ đi kèm (SIL Open Font License) |
| `tests/` | Kiểm thử tự động |

## Giấy phép

MIT (`LICENSE`). Phông chữ và dữ liệu bản đồ: `THIRD-PARTY-NOTICES.md`.

## Tác giả

Phát triển bởi **Viên Thế Hưng**. Góp ý, đề xuất chỉnh sửa: **[vienhung.work@gmail.com](mailto:vienhung.work@gmail.com)** hoặc mục [Issues](https://github.com/h3nry1s/phantrang/issues).
