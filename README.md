# VEGA CINEMAS

Ứng dụng đặt vé **ASP.NET Core MVC / Razor / Bootstrap 5 / EF Core / MySQL / Identity**, chia tầng trong một ứng dụng tập trung. Giao diện tiếng Việt, responsive, nền xanh đen `#0B1020` và đỏ `#E63946`.

## Chạy nhanh trên máy này

Mở **VegaCinemas.sln**, chọn **VegaCinema** làm Startup Project. Web project được giữ tại thư mục gốc để tương thích cấu hình Visual Studio ban đầu.

Hoặc mở PowerShell tại thư mục dự án:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-Local.ps1
```

Script chuẩn bị một MySQL Community 8.4.6 riêng tại `.local/`, dùng cổng loopback **33316**, cấu hình tài khoản riêng, ghim khóa RSA của MySQL cho xác thực cục bộ, chuẩn bị chứng chỉ phát triển dưới `.local/https.pem` và `.local/https.key`, khởi động web và dừng MySQL do script khởi động khi web kết thúc. Không sử dụng database XAMPP hay dịch vụ MySQL khác. Lần đầu cần Internet để tải bản portable khoảng vài trăm MB nếu chưa có. Nếu cổng bị một tiến trình khác chiếm, script dừng thay vì thay đổi tiến trình đó.

- Website: **https://localhost:7175**; HTTP phục vụ phát triển: `http://localhost:5188`.
- Lần đầu tin cậy HTTPS development certificate: `dotnet dev-certs https --trust`.
- Mật khẩu demo được sinh ngẫu nhiên, lưu tại **`.local/demo-access.txt`**; cấu hình riêng ở **`appsettings.Local.json`**. Hai đường dẫn này bị loại khỏi Git.
- `CUSTOMER`: `customer@vega.local`; `ADMIN`: `admin@vega.local`; `CINEMA_STAFF`: `staff@vega.local`; `TICKET_CHECKER`: `checker@vega.local`; `CINEMA_MANAGER`: `manager@vega.local`.
- Staff/checker/manager demo thuộc cụm VEGA Sài Gòn. Chọn VEGA Landmark để thử kiểm vé bằng checker demo.

Để chạy bằng Visual Studio với MySQL portable, giữ MySQL riêng đang chạy, hoặc dùng MySQL/Docker theo hướng dẫn bên dưới. `dotnet run` cần database hoạt động trước khi khởi động.

## Công nghệ và cấu trúc

Yêu cầu SDK .NET 8 trở lên có khả năng target `net8.0`, runtime ASP.NET Core 8, Visual Studio 2022 hỗ trợ .NET 8, MySQL 8.4/InnoDB.

| Project / đường dẫn | Trách nhiệm |
|---|---|
| `VegaCinema.csproj` | Presentation: Controllers, Razor, ViewModels, Areas/Admin, wwwroot |
| `src/CinemaBooking.Domain` | Entity, enum, contract gateway/QR và kiểu lỗi nghiệp vụ |
| `src/CinemaBooking.DataAccess` | VegaDbContext, Identity store, migrations và SeedData |
| `src/CinemaBooking.Business` | Movie/Cinema/Showtime/Booking/SeatHold/Payment/Ticket/User/Report/Admin services |
| `src/CinemaBooking.Integrations` | Mock gateway ký HMAC-SHA256; QR PNG bằng QRCoder |
| `tests/CinemaBooking.Tests` | xUnit, MySQL integration/concurrency và HTTP Identity/CSRF tests |
| `tests/browser-flow.py` | Playwright kiểm tra luồng trình duyệt và responsive |
| `docs/ARCHITECTURE.md` | Giả định nghiệp vụ, phụ thuộc và sơ đồ trạng thái |

.NET 8 là LTS và còn được hỗ trợ đến **10/11/2026**, theo [chính sách Microsoft](https://dotnet.microsoft.com/en-us/platform/support/policy). Bản này giữ .NET 8 để chạy trên SDK/Visual Studio hiện có. Cần lên LTS tiếp theo khi sử dụng sau mốc đó; đồng thời nâng provider tương thích. EF/Identity 8.0.32 đi cùng [Pomelo 8.0.3](https://www.nuget.org/packages/Pomelo.EntityFrameworkCore.MySql/8.0.3), không trộn EF major 9/10.

`NuGet.Config` khai báo nguồn nuget.org riêng. Package được restore tại `.packages/` của checkout để không phụ thuộc quyền ghi vào hồ sơ hệ thống.

## Cấu hình MySQL có sẵn hoặc Docker

Tạo database UTF-8 và user riêng có quyền DDL cho migrations, DML cho runtime. Không đưa mật khẩu thật vào mã nguồn.

```sql
CREATE DATABASE vega_cinemas CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
-- Tạo user và GRANT cho vega_cinemas bằng công cụ quản trị với mật khẩu riêng.
```

Với Docker Desktop đang chạy:

```powershell
Copy-Item .env.example .env
# Điền MYSQL_PASSWORD và MYSQL_ROOT_PASSWORD của bạn trong .env.
docker compose up -d
```

Docker mặc định bind cổng `127.0.0.1:3307`. Database `vega_cinemas`, user `vega_app`. Volume giữ dữ liệu giữa các lần khởi động; không xóa volume chứa dữ liệu cần giữ.

Cấu hình bằng User Secrets (thay các phần trong dấu `<...>` bằng giá trị riêng):

```powershell
dotnet user-secrets set 'ConnectionStrings:VegaDatabase' 'Server=127.0.0.1;Port=3307;Database=vega_cinemas;User=vega_app;Password=<mật khẩu>;SslMode=Preferred' --project VegaCinema.csproj
dotnet user-secrets set 'Payment:SigningKey' '<chuỗi ngẫu nhiên ít nhất 32 ký tự>' --project VegaCinema.csproj
dotnet user-secrets set 'Seed:DemoPassword' '<mật khẩu demo đủ mạnh>' --project VegaCinema.csproj
dotnet user-secrets set 'Database:AutoMigrate' 'true' --project VegaCinema.csproj
dotnet user-secrets set 'Database:SeedDemo' 'true' --project VegaCinema.csproj
dotnet restore VegaCinemas.sln
dotnet build VegaCinemas.sln --no-restore
dotnet run --project VegaCinema.csproj --launch-profile https
```

Nếu có `appsettings.Local.json`, nó ghi đè User Secrets trong Development; đổi hoặc bỏ riêng tệp cấu hình cục bộ khi chuyển sang database khác. Biến môi trường có ưu tiên cao nhất, ví dụ `ConnectionStrings__VegaDatabase`, `Payment__SigningKey`, `Seed__DemoPassword`, `Database__AutoMigrate`.

Trên database production: cấp quyền runtime tối thiểu, dùng HTTPS và MySQL TLS được xác minh, đặt `Database:AutoMigrate=false`, `Database:SeedDemo=false`; triển khai migration riêng. Không lưu thông tin thẻ. Signing key phải ổn định qua lần khởi động để đối soát được xác nhận cũ. Chế độ Development có khóa cookie riêng dưới `.local/keys`; production dùng cơ chế Data Protection của host và cần lưu/bảo vệ key ring phù hợp.

## Migrations và dữ liệu demo

Đã có migrations tạo đầy đủ Identity và 13 entity nghiệp vụ; các index unique cho `(ShowtimeId, SeatId)`, mã booking, mã giao dịch, mã vé, `BookingDetailId` trên vé; mọi tiền tệ là `decimal(14,2)`.

Nếu không dùng AutoMigrate:

```powershell
dotnet tool install dotnet-ef --tool-path .tools --version 8.0.32
.\.tools\dotnet-ef.exe database update --project src/CinemaBooking.DataAccess --startup-project VegaCinema.csproj
```

Danh mục hiện dùng **toàn bộ 24 poster trong thư mục `Phim/`**, giữ nguyên ảnh gốc và tên phim từ tên tệp. Bản web nằm ở `wwwroot/images/movies/`; manifest nhúng trong DataAccess nằm ở `src/CinemaBooking.DataAccess/SeedData/movie-posters.json`. Trang chủ hiển thị đủ danh mục, banner dùng các poster và trang danh sách có phân trang.

Thông tin phim ngoài poster/tên chưa được cung cấp: thể loại tổng quát, thời lượng 120 phút, ngày khởi chiếu và lịch chiếu là **dữ liệu demo**, phân loại tuổi ghi “Chưa rõ”. Có thể cập nhật qua Admin. Seed chỉ đồng bộ poster cho bản ghi đã tồn tại, giữ nguyên thông tin và trạng thái do Admin chỉnh sửa.

Seed tạo 2 cụm rạp, 2 rạp, 4 phòng × 96 ghế, ghế thường/VIP và bổ sung các khung giờ demo còn trống trong 7 ngày tới (tối đa 140 suất trên 4 phòng). Khi nâng cấp từ bộ phim SVG cũ, các phim mẫu nguyên bản được lưu trữ; booking, vé, tên phim và poster trong lịch sử được giữ nguyên. Chỉ các suất cũ ở tương lai chưa có booking được ngừng nhận đặt để dành khung giờ cho danh mục mới. Lịch mới được kiểm tra chồng lấn với các suất còn hoạt động.

Khi bổ sung ảnh vào `Phim/`, chạy `python scripts/import-movie-posters.py`, build và khởi động lại với `Database:SeedDemo=true` cùng `Database:AutoMigrate=true`. Script sao chép tất cả ảnh và kiểm tra nội dung không thay đổi; seed đồng bộ vào database, không tạo trùng khi chạy lại. Các SVG minh họa cũ vẫn được giữ để hiển thị lịch sử giao dịch.

Để tạo **10 cụm / 100 phòng**: chọn một database demo **mới**, bật `Seed:Large=true` trước lần seed đầu. Cờ này không thay đổi cấu trúc một database đã seed. Mỗi lần khởi động demo, seed bổ sung các khung giờ còn trống trong 7 ngày tới và giữ lịch đã có. Seed chỉ tạo tài khoản demo khi có `Seed:DemoPassword`, không đổi mật khẩu tài khoản đã tồn tại.

## Thử các chức năng

1. Mở trang chủ → tìm/lọc phim hoặc đặt vé nhanh → chọn suất.
2. Đăng nhập customer → chọn tối đa 8 ghế → tiếp tục. Đến bước này backend mới giữ ghế.
3. Checkout hiển thị countdown dựa trên `serverNowUtc` và `expiresAtUtc`; chọn SUCCESS, FAILED hoặc TIMEOUT.
4. SUCCESS tạo một vé/ghế; xem QR và dùng **In / Lưu vé PDF**. FAILED/TIMEOUT không tạo vé và trả ghế về trống.
5. Đăng nhập checker → menu Kiểm soát vé → nhập mã hoặc dùng máy quét QR dạng bàn phím → xác nhận. Vé USED không dùng lại được.
6. Customer xem lịch sử, hồ sơ, đổi mật khẩu; hủy cả đơn theo chính sách (vé chưa dùng, cách giờ chiếu ≥2 giờ). Hoàn tiền là bản ghi mô phỏng.
7. Admin có sidebar quản lý phim/cụm/rạp/phòng/ghế/suất, user/vai trò/phạm vi, tra cứu booking/payment/ticket, nhật ký và báo cáo. Các danh sách có tìm kiếm, lọc và phân trang; sửa trường có validation server.
8. Cấu hình ghế trước khi tạo suất; phòng đã có lịch giữ nguyên sơ đồ để bảo toàn lịch sử. Ngừng hoạt động thay cho xóa dữ liệu đã được tham chiếu.
9. Báo cáo đếm các đơn đã phát hành vé chưa hủy theo thời điểm tạo booking; manager chỉ xem cụm được gán. Đơn thuộc cụm khác không truy cập được bằng staff/checker.

Giao dịch có xác nhận thành công nhưng lần phát hành gặp lỗi vẫn giữ bằng chứng gateway trong Payment. Admin mở chi tiết đơn, dùng **Xử lý lại xác nhận** với giao dịch còn PENDING có bằng chứng. Nếu ghế đã hết hạn hoặc được cấp cho người khác, payment chuyển `REFUND_REQUIRED`; cần đối soát thủ công, không cấp lại ghế. Đây là thanh toán mô phỏng, chưa kết nối ngân hàng thực.

## Kiểm thử

Đặt `VEGA_TEST_MYSQL` bằng connection string của tài khoản có quyền tạo/xóa **database kiểm thử**. Fixture tự tạo tên `vega_test_<GUID>`, chạy migrations thật và chỉ xóa database ngẫu nhiên do nó tạo. Không dùng InMemory, không tự bỏ qua khi thiếu cấu hình.

```powershell
$env:VEGA_TEST_MYSQL='Server=127.0.0.1;Port=3307;User=<test user>;Password=<test password>;SslMode=Preferred'
dotnet test tests/CinemaBooking.Tests/CinemaBooking.Tests.csproj --logger 'trx;LogFileName=mysql-integration.trx'
```

TC01–TC09 gồm thành công, **100 DbContext độc lập cùng giữ một ghế**, hết hạn, thất bại, callback lặp đồng thời, callback thành công đến muộn sau khi ghế bán lại, vé sử dụng một lần, Identity/role/ownership/CSRF, xung đột lịch phòng. Có thêm kiểm tra rollback toàn bộ nhóm ghế, chữ ký/số tiền giả mạo và render trang Razor trên MySQL.

Browser QA khi web và demo account đã chạy:

```powershell
python -m pip install playwright
python tests/browser-flow.py
```

Script mặc định dùng Edge đã cài (không cần tải Chromium); có thể đổi `VEGA_BROWSER`, `VEGA_BASE_URL`, `VEGA_DEMO_PASSWORD`. Nó tạo các đơn thanh toán mô phỏng và một cụm rạp QA ngừng hoạt động, vì vậy chỉ chạy trên **database demo**. Ảnh và kết quả lưu tại `.local/qa/`.

## Giới hạn xác nhận

- Chưa có UML gốc ngoài yêu cầu đính kèm; tài liệu thiết kế suy ra từ yêu cầu, chưa đối chiếu được với bản vẽ đồ án riêng.
- P95 dưới 1/2/5 giây, uptime 99,5% và 5.000–10.000 booking/ngày là mục tiêu cần benchmark/hạ tầng; không coi kiểm thử chức năng là bằng chứng đạt SLA.
- QR được đọc bằng máy quét ngoài hoặc nhập mã; chưa có quét camera trực tiếp trong browser.
- Gateway và hoàn tiền hiện là mô phỏng. Không có email gửi vé hay cổng thanh toán thật vì không có thông tin tích hợp trong đề bài.
#   V e g a C i n e m a  
 