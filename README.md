# Phấn Trắng

Bảng xanh trên mọi màn hình: công cụ giảng dạy miễn phí cho thầy cô trên Windows 10 và 11. Chỉ bài bằng viên phấn, viết như tay, vẽ, chấm bài bằng Điểm 10 ngay trên slide, PDF hay trang web đang chiếu.

Giới thiệu và thử từng tính năng: **https://phantrang.org**

## Tải về và cài đặt

**Cài từ Microsoft Store (cách chính):** **[Phấn Trắng trên Microsoft Store](https://apps.microsoft.com/detail/9N4CWVRK45FF?hl=vi-VN&gl=VN)**. Mở trang, bấm cài đặt. Bản Store được Microsoft kiểm duyệt nên Windows không cảnh báo, Smart App Control không chặn, và ứng dụng tự cập nhật khi có bản mới. Gỡ cài đặt: **Cài đặt Windows › Ứng dụng › Phấn Trắng › Gỡ cài đặt**.

### Cách phụ: tải từ GitHub (máy không dùng được Microsoft Store, hoặc cần bản chạy từ USB)

Trang **[Releases](https://github.com/h3nry1s/phantrang/releases/latest)** vẫn có:

- **`PhanTrang-Setup-<phiên bản>.exe`**: mở tệp, bấm **Cài đặt** (không cần quyền quản trị, không cần mạng). Bản tự lưu và cài đặt nằm trong `%APPDATA%\PhanTrang`, gỡ cài đặt vẫn giữ lại.
- **`PhanTrang-<phiên bản>-portable.zip`**: chạy không cần cài. Bấm chuột phải › **Extract All…** (Giải nén tất cả), chép thư mục ra máy hoặc USB, mở **`PhanTrang.exe`** (thư mục `assets` phải nằm cạnh tệp exe).

Bản tải từ GitHub **chưa có chữ ký số**, nên:

- **“Windows đã bảo vệ máy tính của bạn”** (SmartScreen): bấm **Thông tin thêm** › **Vẫn chạy**.
- **Smart App Control chặn ứng dụng** (máy Windows 11 cài mới): Windows không cho bỏ qua, hãy cài bản trên **Microsoft Store**.

Kiểm tra tệp tải về đúng là bản phát hành: mã SHA-256 ghi trong phần mô tả của mỗi bản phát hành và trong `phien-ban.json`. Trong PowerShell: `Get-FileHash <tệp>`.

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

Bản tự build không có chữ ký số, nên máy bật **Smart App Control** có thể không cho chạy; khi đó dùng bản trên Microsoft Store hoặc bản phát hành có chữ ký.

## Dành cho người phát triển

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1               # dist\PhanTrang.exe
powershell -ExecutionPolicy Bypass -File tests\run-tests.ps1     # build + kiểm thử tự động
powershell -ExecutionPolicy Bypass -File release.ps1 -AllowUnsigned   # bộ cài + bản zip (dist\release)
powershell -ExecutionPolicy Bypass -File pack-msix.ps1           # gói Microsoft Store
```

Chỉ cần Windows: script dùng `csc.exe` (.NET Framework 4.x) có sẵn, không cần Visual Studio. GitHub Actions tự build và kiểm thử mỗi lần đẩy mã; gắn tag `v…` để tạo bản phát hành. Ký số: xem `CODE-SIGNING.md`.

| Thư mục | Nội dung |
|---|---|
| `src/` | Mã nguồn ứng dụng (C#, WinForms) |
| `installer/` | Bộ cài cho từng tài khoản |
| `assets/fonts/` | Phông chữ đi kèm (SIL Open Font License) |
| `tests/` | Kiểm thử tự động |
| `site/` | Chính sách quyền riêng tư (GitHub Pages); trang giới thiệu ở [phantrang.org](https://phantrang.org) |

## Giấy phép

MIT (`LICENSE`). Phông chữ và dữ liệu bản đồ: `THIRD-PARTY-NOTICES.md`.

## Tác giả

Phát triển bởi **Viên Thế Hưng**. Góp ý, đề xuất chỉnh sửa: **[vienhung.work@gmail.com](mailto:vienhung.work@gmail.com)** hoặc mục [Issues](https://github.com/h3nry1s/phantrang/issues).
