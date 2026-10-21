# Kết quả kiểm tra · 21/10/2026

## Cập nhật bộ poster từ Phim/
- Đã sao chép **24/24 ảnh** sang `wwwroot/images/movies/`, kiểm tra SHA-256 trùng với ảnh gốc.
- Database có 24 phim dùng 24 poster khác nhau, cả 24 có suất chiếu sắp tới trong lịch demo.
- Trang chủ hiển thị đủ 24 thẻ phim; danh sách có 2 trang × 12 phim; cả 24 trang chi tiết có poster mới và liên kết đặt vé.
- Tất cả 24 URL ảnh trả HTTP 200, nội dung trùng với ảnh đã sao chép; trình duyệt giải mã được toàn bộ ảnh.
- Banner dùng bộ poster mới; kiểm tra desktop 1440px và mobile 390px không tràn ngang.
- Đối chiếu snapshot database trước/sau nhập: ID, mã đơn, chủ đơn, suất, tổng tiền, tên/poster phim trong lịch sử cùng mã/trạng thái vé được giữ nguyên.
- Browser suite đặt vé, thanh toán, kiểm vé, phân quyền và quản trị đã chạy lại thành công với danh mục mới; không ghi nhận lỗi JavaScript.
- Bằng chứng: `.local/qa/poster-results.json`, `posters-home-desktop.png`, `posters-home-mobile.png`.

## Môi trường
- Windows, SDK .NET 9.0.318 target `net8.0`, ASP.NET Core runtime 8.0.31.
- EF Core / Identity 8.0.32; Pomelo MySQL 8.0.3.
- **MySQL Community 8.4.6 thật**, InnoDB, cổng loopback 33316; không dùng InMemory/SQLite/MariaDB.
- Microsoft Edge headless qua Playwright; desktop 1440px, mobile 390px.

## Kết quả
| Kiểm tra | Kết quả |
|---|---|
| Build solution | Thành công, 0 cảnh báo / 0 lỗi |
| Release publish | Thành công; không chứa appsettings.Local.json hoặc thư mục runtime .local |
| Migrations | Áp dụng được trên database mới; không có thay đổi model chưa được migration |
| xUnit integration/HTTP | **18 passed, 0 failed, 0 skipped** |
| Tranh chấp 100 request / 100 DbContext | Đúng 1 lượt giữ thành công |
| Callback thành công gửi lặp đồng thời | Một giao dịch và đúng số vé; không phát hành thêm |
| Callback muộn sau khi ghế bán lại | Giữ quyền của đơn mới; đơn cũ không có vé, payment REFUND_REQUIRED |
| Lỗi lưu vé sau xác nhận gateway | Lưu được bằng chứng; retry phát hành đúng 1 vé |
| 5 mật khẩu sai | Tài khoản khóa tối thiểu 15 phút |
| Browser HTTPS | Qua toàn bộ luồng với https://localhost:7175 |
| JavaScript | Không ghi nhận lỗi pageerror trong browser suite |
| Mobile | Các trang chính và trang chọn ghế không tràn ngang; sơ đồ ghế cuộn trong khung |
| Script Start-Local.ps1 | Khởi động MySQL riêng, tạo cấu hình cục bộ, chạy website HTTPS trả HTTP 200 |

Browser suite kiểm tra đặt hai ghế → giữ → countdown → thanh toán → hai vé QR PNG; checker xác nhận USED và không có thao tác xác nhận lần hai; customer bị từ chối Admin; 11 danh sách/7 editor Admin; tạo và ngừng một cụm rạp QA; báo cáo manager; tra cứu staff; đăng ký/hồ sơ/đổi mật khẩu/đăng nhập lại; menu mobile.

## Bằng chứng cục bộ
- `tests/CinemaBooking.Tests/TestResults/mysql-integration.trx`
- `.local/qa/browser-results.json`
- `.local/qa/home-desktop.png`, `home-mobile.png`, `seats-desktop.png`, `checkout-desktop.png`, `tickets-desktop.png`, `admin-desktop.png`

Các tệp runtime, ảnh QA và dữ liệu truy cập bị loại khỏi Git; cách chạy lại nằm trong README. Browser suite tạo dữ liệu giao dịch mô phỏng nên chỉ chạy trên database demo.

## Chưa xác nhận
- Chưa benchmark P95, SLA uptime hoặc tải 5.000–10.000 booking/ngày.
- Chưa chạy seed lớn 10 cụm/100 phòng để đo thời gian và bộ nhớ.
- Chưa đối chiếu UML gốc vì chưa có tệp sơ đồ; các sơ đồ trong ARCHITECTURE.md được suy ra từ yêu cầu.
- Chưa tích hợp cổng thanh toán ngân hàng thật; không có quét camera trực tiếp trong trình duyệt. QR hỗ trợ máy quét dạng bàn phím hoặc nhập mã.
