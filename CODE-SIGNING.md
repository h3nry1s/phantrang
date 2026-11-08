# Code signing policy / Chính sách ký số

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).
*(Dòng trên chỉ đúng sau khi dự án được SignPath Foundation chấp nhận. Trước đó, bản phát hành chưa được ký.)*

## Những gì được ký

Chỉ các tệp build từ mã nguồn trong repository này, bằng workflow GitHub Actions `.github/workflows/build.yml`:

- `PhanTrang.exe` (ứng dụng)
- `PhanTrang-Setup-<phiên bản>.exe` (bộ cài)

Không ký tệp nào build trên máy cá nhân, và không ký phần mềm của bên thứ ba.

## Vai trò

| Vai trò | Người |
|---|---|
| Tác giả, người duyệt (committer, reviewer) | Viên Thế Hưng ([@h3nry1s](https://github.com/h3nry1s)) |
| Người phê duyệt ký (approver) | Viên Thế Hưng ([@h3nry1s](https://github.com/h3nry1s)) |

Mọi thành viên có quyền trên repository và trên SignPath đều bật xác thực hai lớp (2FA).

Mỗi lần ký đều cần người phê duyệt bấm duyệt trên SignPath, sau khi xem lại các thay đổi kể từ bản trước.

## Quyền riêng tư

Phấn Trắng không gửi dữ liệu nào ra mạng. Ứng dụng không có tính năng tự cập nhật, đo lường hay thống kê sử dụng. Cài đặt, nhật ký lỗi và bài giảng chỉ nằm trên máy của người dùng, trong `%APPDATA%\PhanTrang` và những thư mục người dùng tự chọn khi lưu.

## Ký trên máy người bảo trì (khi có chứng chỉ riêng)

Nếu dùng chứng chỉ ký mã cá nhân (ví dụ Certum qua SimplySign) thay cho SignPath: đăng nhập SimplySign Desktop để chứng chỉ hiện trong Windows, rồi chạy `build.ps1` hoặc `release.ps1`. Hai script tự gọi `sign.ps1` (SHA-256, có dấu thời gian) khi thấy chứng chỉ; `release.ps1` không phát hành bản chưa ký trừ khi thêm `-AllowUnsigned`.

## Báo lỗi bảo mật

Nếu bạn phát hiện bản phát hành có chữ ký nhưng không khớp với mã nguồn, hoặc có lỗi bảo mật, hãy báo qua mục *Security → Report a vulnerability* của repository trên GitHub. Không đăng công khai trong Issues.
