using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CinemaBooking.DataAccess;
using CinemaBooking.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace CinemaBooking.Business.Services;

public record AdminRow(string Id, string[] Cells);
public record AdminTable(string Section, string Title, string[] Columns, List<AdminRow> Rows, int Total, int Page, bool Editable);
public record FieldOption(string Value, string Text);
public record AdminField(string Name, string Label, string Type, string Value, bool Required = true, List<FieldOption>? Options = null);
public record AdminEditor(string Section, string Title, string Id, List<AdminField> Fields);

public class AdminService(VegaDbContext db, MovieService movies, ShowtimeService shows, UserService userService, UserManager<ApplicationUser> users, TimeProvider clock)
{
    public static readonly Dictionary<string, string> Sections = new()
    { ["movies"] = "Phim", ["clusters"] = "Cụm rạp", ["cinemas"] = "Rạp chiếu", ["rooms"] = "Phòng chiếu", ["seats"] = "Sơ đồ ghế", ["showtimes"] = "Suất chiếu", ["bookings"] = "Đơn đặt vé", ["payments"] = "Thanh toán", ["tickets"] = "Vé điện tử", ["users"] = "Người dùng & phân quyền", ["logs"] = "Nhật ký hoạt động" };
    public async Task<AdminTable> ListAsync(string section, string? search, string? filter, int page)
    {
        if (!Sections.TryGetValue(section, out var title)) throw new BusinessException("Mục quản trị không tồn tại.");
        page = Math.Max(1, page); var term = search?.Trim() ?? "";
        async Task<AdminTable> Table<T>(IQueryable<T> query, string[] columns, Func<T, AdminRow> row, bool editable)
        { var count = await query.CountAsync(); var items = await query.Skip((page - 1) * 15).Take(15).ToListAsync(); return new(section, title, columns, items.Select(row).ToList(), count, page, editable); }
        var active = filter != "inactive";
        Enum.TryParse<BookingStatus>(filter, out var bookingState);
        Enum.TryParse<PaymentStatus>(filter, out var paymentState);
        Enum.TryParse<TicketStatus>(filter, out var ticketState);
        return section switch
        {
            "movies" => await Table(db.Movies.AsNoTracking().Where(x => x.Title.Contains(term) && (filter == null || filter == "" || (active ? x.Status != MovieStatus.ARCHIVED : x.Status == MovieStatus.ARCHIVED))).OrderBy(x => x.Id), ["Tên phim", "Thể loại", "Thời lượng", "Trạng thái"], x => new(x.Id.ToString(), [x.Title, x.Genre, $"{x.DurationMinutes} phút", x.Status.ToString()]), true),
            "clusters" => await Table(db.CinemaClusters.AsNoTracking().Where(x => x.Name.Contains(term) && (string.IsNullOrEmpty(filter) || x.IsActive == active)).OrderBy(x => x.Id), ["Cụm rạp", "Thành phố", "Hoạt động"], x => new(x.Id.ToString(), [x.Name, x.City, On(x.IsActive)]), true),
            "cinemas" => await Table(db.Cinemas.AsNoTracking().Include(x => x.CinemaCluster).Where(x => x.Name.Contains(term) && (string.IsNullOrEmpty(filter) || x.IsActive == active)).OrderBy(x => x.Id), ["Tên rạp", "Cụm rạp", "Địa chỉ", "Hoạt động"], x => new(x.Id.ToString(), [x.Name, x.CinemaCluster.Name, x.Address, On(x.IsActive)]), true),
            "rooms" => await Table(db.ScreeningRooms.AsNoTracking().Include(x => x.Cinema).Where(x => (x.Name.Contains(term) || x.Cinema.Name.Contains(term)) && (string.IsNullOrEmpty(filter) || x.IsActive == active)).OrderBy(x => x.Id), ["Phòng", "Rạp", "Sơ đồ", "Hoạt động"], x => new(x.Id.ToString(), [x.Name, x.Cinema.Name, $"{x.RowCount} × {x.ColumnCount}", On(x.IsActive)]), true),
            "seats" => await Table(db.Seats.AsNoTracking().Include(x => x.ScreeningRoom).Where(x => (x.Row.Contains(term) || x.ScreeningRoom.Name.Contains(term)) && (string.IsNullOrEmpty(filter) || x.IsActive == active)).OrderBy(x => x.ScreeningRoomId).ThenBy(x => x.Row).ThenBy(x => x.Number), ["Ghế", "Phòng", "Loại", "Hoạt động"], x => new(x.Id.ToString(), [x.Label, x.ScreeningRoom.Name, x.Type.ToString(), On(x.IsActive)]), true),
            "showtimes" => await Table(db.Showtimes.AsNoTracking().Include(x => x.Movie).Include(x => x.ScreeningRoom).Where(x => x.Movie.Title.Contains(term) && (string.IsNullOrEmpty(filter) || x.IsActive == active)).OrderByDescending(x => x.StartsAtUtc), ["Phim", "Phòng", "Thời gian", "Giá thường / VIP", "Hoạt động"], x => new(x.Id.ToString(), [x.Movie.Title, x.ScreeningRoom.Name, VietnamTime.Local(x.StartsAtUtc).ToString("dd/MM/yyyy HH:mm"), $"{VietnamTime.Money(x.StandardPrice)} / {VietnamTime.Money(x.VipPrice)}", On(x.IsActive)]), true),
            "bookings" => await Table(db.Bookings.AsNoTracking().Include(x => x.User).Where(x => (x.Code.Contains(term) || x.User.Email!.Contains(term)) && (string.IsNullOrEmpty(filter) || x.Status == bookingState)).OrderByDescending(x => x.CreatedAtUtc), ["Mã đơn", "Khách hàng", "Trạng thái", "Tổng tiền"], x => new(x.Id.ToString(), [x.Code, x.User.Email ?? "", x.Status.ToString(), VietnamTime.Money(x.Total)]), false),
            "payments" => await Table(db.Payments.AsNoTracking().Where(x => x.TransactionCode.Contains(term) && (string.IsNullOrEmpty(filter) || x.Status == paymentState)).OrderByDescending(x => x.CreatedAtUtc), ["Mã giao dịch", "Đơn", "Kết quả", "Số tiền"], x => new(x.BookingId.ToString(), [x.TransactionCode, x.BookingId.ToString(), x.Status.ToString(), VietnamTime.Money(x.Amount)]), false),
            "tickets" => await Table(db.Tickets.AsNoTracking().Include(x => x.BookingDetail).Where(x => x.Code.Contains(term) && (string.IsNullOrEmpty(filter) || x.Status == ticketState)).OrderByDescending(x => x.IssuedAtUtc), ["Mã vé", "Trạng thái", "Phát hành"], x => new(x.BookingDetail.BookingId.ToString(), [x.Code, x.Status.ToString(), VietnamTime.Local(x.IssuedAtUtc).ToString("dd/MM/yyyy HH:mm")]), false),
            "users" => await Table(db.Users.AsNoTracking().Include(x => x.CinemaCluster).Where(x => (x.Email!.Contains(term) || x.FullName.Contains(term)) && (string.IsNullOrEmpty(filter) || x.IsActive == active)).OrderBy(x => x.Email), ["Họ tên", "Email", "Phạm vi", "Hoạt động"], x => new(x.Id, [x.FullName, x.Email ?? "", x.CinemaCluster?.Name ?? "Toàn hệ thống / khách hàng", On(x.IsActive)]), true),
            "logs" => await Table(db.TransactionLogs.AsNoTracking().Where(x => x.Event.Contains(term) || x.Message.Contains(term)).OrderByDescending(x => x.CreatedAtUtc), ["Thời gian", "Sự kiện", "Nội dung"], x => new(x.BookingId?.ToString() ?? "", [VietnamTime.Local(x.CreatedAtUtc).ToString("dd/MM/yyyy HH:mm:ss"), x.Event, x.Message]), false),
            _ => throw new BusinessException("Mục không tồn tại.")
        };
    }
    public async Task<AdminEditor> EditorAsync(string section, string? id)
    {
        var fields = new List<AdminField>(); var number = int.TryParse(id, out var n) ? n : 0;
        void Text(string name, string label, object? value, string type = "text", bool required = true) => fields.Add(new(name, label, type, value?.ToString() ?? "", required));
        void Select(string name, string label, object? value, IEnumerable<FieldOption> options) => fields.Add(new(name, label, "select", value?.ToString() ?? "", true, options.ToList()));
        void Enabled(bool value) => Select("active", "Hoạt động", value ? "true" : "false", [new("true", "Đang hoạt động"), new("false", "Ngừng hoạt động")]);
        async Task<List<FieldOption>> Clusters() => await db.CinemaClusters.OrderBy(x => x.Name).Select(x => new FieldOption(x.Id.ToString(), x.Name)).ToListAsync();
        switch (section)
        {
            case "movies":
                var m = number == 0 ? new Movie { DurationMinutes = 100, ReleaseDate = DateTime.Today } : await db.Movies.FindAsync(number) ?? throw new BusinessException("Không tìm thấy phim.");
                Text("title", "Tên phim", m.Title); Text("genre", "Thể loại", m.Genre); Text("duration", "Thời lượng (phút)", m.DurationMinutes, "number");
                Text("description", "Nội dung", m.Description, "textarea"); Text("poster", "Poster (/images/…)", m.PosterUrl); Text("age", "Phân loại độ tuổi", m.AgeRating);
                Text("release", "Khởi chiếu", m.ReleaseDate.ToString("yyyy-MM-dd"), "date"); Select("status", "Trạng thái", m.Status, Enum.GetNames<MovieStatus>().Select(x => new FieldOption(x, x))); break;
            case "clusters":
                var c = number == 0 ? new CinemaCluster() : await db.CinemaClusters.FindAsync(number) ?? throw new BusinessException("Không tìm thấy cụm.");
                Text("name", "Tên cụm rạp", c.Name); Text("city", "Thành phố", c.City); Enabled(c.IsActive); break;
            case "cinemas":
                var cinema = number == 0 ? new Cinema() : await db.Cinemas.FindAsync(number) ?? throw new BusinessException("Không tìm thấy rạp.");
                Text("name", "Tên rạp", cinema.Name); Text("address", "Địa chỉ", cinema.Address); Select("cluster", "Cụm rạp", cinema.CinemaClusterId, await Clusters()); Enabled(cinema.IsActive); break;
            case "rooms":
                var room = number == 0 ? new ScreeningRoom() : await db.ScreeningRooms.FindAsync(number) ?? throw new BusinessException("Không tìm thấy phòng.");
                Text("name", "Tên phòng", room.Name); Select("cinema", "Rạp", room.CinemaId, await db.Cinemas.Select(x => new FieldOption(x.Id.ToString(), x.Name)).ToListAsync());
                Text("rows", "Số hàng (1–26)", room.RowCount, "number"); Text("columns", "Số ghế / hàng (1–24)", room.ColumnCount, "number"); Enabled(room.IsActive); break;
            case "seats":
                var seat = number == 0 ? new Seat { Number = 1 } : await db.Seats.FindAsync(number) ?? throw new BusinessException("Không tìm thấy ghế.");
                Select("room", "Phòng", seat.ScreeningRoomId, await db.ScreeningRooms.Select(x => new FieldOption(x.Id.ToString(), x.Cinema.Name + " / " + x.Name)).ToListAsync());
                Text("row", "Hàng ghế (A–Z)", seat.Row); Text("number", "Số ghế", seat.Number, "number"); Select("type", "Loại ghế", seat.Type, Enum.GetNames<SeatType>().Select(x => new FieldOption(x, x))); Enabled(seat.IsActive); break;
            case "showtimes":
                var s = number == 0 ? new Showtime { StartsAtUtc = clock.GetUtcNow().UtcDateTime.AddDays(1), StandardPrice = 85000, VipPrice = 110000 } : await db.Showtimes.FindAsync(number) ?? throw new BusinessException("Không tìm thấy suất.");
                Select("movie", "Phim", s.MovieId, await db.Movies.Where(x => x.Status != MovieStatus.ARCHIVED).Select(x => new FieldOption(x.Id.ToString(), x.Title)).ToListAsync());
                Select("room", "Phòng", s.ScreeningRoomId, await db.ScreeningRooms.Where(x => x.IsActive).Select(x => new FieldOption(x.Id.ToString(), x.Cinema.Name + " / " + x.Name)).ToListAsync());
                Text("starts", "Bắt đầu (giờ Việt Nam)", VietnamTime.Local(s.StartsAtUtc).ToString("yyyy-MM-ddTHH:mm"), "datetime-local"); Text("price", "Giá thường", s.StandardPrice.ToString("F0", CultureInfo.InvariantCulture), "number"); Text("vip", "Giá VIP", s.VipPrice.ToString("F0", CultureInfo.InvariantCulture), "number"); Enabled(s.IsActive); break;
            case "users":
                var user = string.IsNullOrWhiteSpace(id) ? new ApplicationUser() : await users.FindByIdAsync(id) ?? throw new BusinessException("Không tìm thấy tài khoản.");
                Text("name", "Họ tên", user.FullName); Text("email", "Email", user.Email, "email"); Text("password", "Mật khẩu (chỉ khi tạo mới)", "", "password", string.IsNullOrWhiteSpace(id));
                Select("role", "Vai trò", string.IsNullOrWhiteSpace(id) ? Roles.Customer : (await users.GetRolesAsync(user)).FirstOrDefault(), Roles.All.Select(x => new FieldOption(x, x)));
                Select("cluster", "Phạm vi cụm rạp", user.CinemaClusterId, new[] { new FieldOption("", "Không gán (Admin / khách hàng)") }.Concat(await Clusters())); Enabled(user.IsActive); break;
            default: throw new BusinessException("Mục này chỉ hỗ trợ tra cứu.");
        }
        return new(section, Sections[section], id ?? "", fields);
    }
    public async Task SaveAsync(string section, string? id, Dictionary<string, string> values, string actorId)
    {
        string V(string key, int max = 250) { var v = values.GetValueOrDefault(key, "").Trim(); if (v.Length > max) throw new BusinessException($"Trường {key} tối đa {max} ký tự."); return v; }
        int I(string key) => int.TryParse(V(key), out var value) ? value : throw new BusinessException($"Trường {key} cần là số nguyên.");
        decimal D(string key) => decimal.TryParse(V(key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value < 10000000 ? value : throw new BusinessException("Giá vé không hợp lệ.");
        T E<T>(string key) where T : struct, Enum => Enum.TryParse<T>(V(key), out var value) && Enum.IsDefined(value) ? value : throw new BusinessException("Lựa chọn không hợp lệ.");
        bool active = V("active") == "true"; var number = int.TryParse(id, out var parsed) ? parsed : 0;
        void Validate(object entity) { var errors = new List<ValidationResult>(); if (!Validator.TryValidateObject(entity, new ValidationContext(entity), errors, true)) throw new BusinessException("Thông tin chưa hợp lệ. Kiểm tra trường bắt buộc, độ dài và khoảng giá trị."); }
        switch (section)
        {
            case "movies":
                if (!DateTime.TryParse(V("release"), out var release)) throw new BusinessException("Ngày khởi chiếu không hợp lệ.");
                var m = new Movie { Id = number, Title = V("title", 160), Genre = V("genre", 100), DurationMinutes = I("duration"), Description = V("description", 4000), PosterUrl = V("poster"), AgeRating = V("age", 10), ReleaseDate = release, Status = E<MovieStatus>("status") };
                Validate(m); await movies.SaveAsync(m); break;
            case "clusters":
                var cluster = number == 0 ? new CinemaCluster() : await db.CinemaClusters.FindAsync(number) ?? throw new BusinessException("Không tìm thấy cụm.");
                cluster.Name = V("name", 120); cluster.City = V("city", 80); cluster.IsActive = active; Validate(cluster); if (number == 0) db.Add(cluster); break;
            case "cinemas":
                var cinema = number == 0 ? new Cinema() : await db.Cinemas.FindAsync(number) ?? throw new BusinessException("Không tìm thấy rạp.");
                var targetCluster = I("cluster");
                if (!await db.CinemaClusters.AnyAsync(x => x.Id == targetCluster)) throw new BusinessException("Cụm rạp không tồn tại.");
                cinema.Name = V("name", 120); cinema.Address = V("address"); cinema.CinemaClusterId = I("cluster"); cinema.IsActive = active; Validate(cinema); if (number == 0) db.Add(cinema); break;
            case "rooms":
                var room = number == 0 ? new ScreeningRoom() : await db.ScreeningRooms.FindAsync(number) ?? throw new BusinessException("Không tìm thấy phòng.");
                var rows = I("rows"); var columns = I("columns"); var cinemaId = I("cinema");
                if (!await db.Cinemas.AnyAsync(x => x.Id == cinemaId)) throw new BusinessException("Rạp không tồn tại.");
                var changed = room.RowCount != rows || room.ColumnCount != columns;
                if (number != 0 && await db.Showtimes.AnyAsync(x => x.ScreeningRoomId == number) && (changed || room.CinemaId != cinemaId)) throw new BusinessException("Phòng đã có suất chiếu; chỉ được đổi tên hoặc trạng thái.");
                room.Name = V("name", 80); room.CinemaId = cinemaId; room.RowCount = rows; room.ColumnCount = columns; room.IsActive = active; Validate(room);
                await using (var tx = await db.Database.BeginTransactionAsync())
                {
                    if (number == 0) db.Add(room); await db.SaveChangesAsync();
                    if (changed || number == 0)
                    {
                        await db.Seats.Where(x => x.ScreeningRoomId == room.Id).ExecuteDeleteAsync();
                        for (var r = 0; r < rows; r++) for (var col = 1; col <= columns; col++) db.Seats.Add(new Seat { ScreeningRoomId = room.Id, Row = ((char)('A' + r)).ToString(), Number = col, Type = r >= rows / 2 ? SeatType.VIP : SeatType.STANDARD });
                        await db.SaveChangesAsync();
                    }
                    await tx.CommitAsync();
                } break;
            case "seats":
                var seat = number == 0 ? new Seat() : await db.Seats.FindAsync(number) ?? throw new BusinessException("Không tìm thấy ghế.");
                var roomId = I("room");
                if (await db.Showtimes.AnyAsync(x => x.ScreeningRoomId == roomId || x.ScreeningRoomId == seat.ScreeningRoomId)) throw new BusinessException("Cấu hình ghế trước khi tạo suất chiếu. Phòng đã có lịch cần được giữ nguyên để bảo toàn dữ liệu.");
                var parent = await db.ScreeningRooms.FindAsync(roomId) ?? throw new BusinessException("Phòng không tồn tại.");
                var row = V("row").ToUpperInvariant(); var colNo = I("number");
                if (row.Length != 1 || row[0] < 'A' || row[0] >= 'A' + parent.RowCount || colNo < 1 || colNo > parent.ColumnCount) throw new BusinessException("Vị trí ghế vượt cấu hình phòng.");
                if (await db.Seats.AnyAsync(x => x.Id != number && x.ScreeningRoomId == roomId && x.Row == row && x.Number == colNo)) throw new BusinessException("Vị trí ghế đã tồn tại.");
                seat.ScreeningRoomId = roomId; seat.Row = row; seat.Number = colNo; seat.Type = E<SeatType>("type"); seat.IsActive = active; if (number == 0) db.Add(seat); break;
            case "showtimes":
                if (!DateTime.TryParse(V("starts"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)) throw new BusinessException("Thời gian không hợp lệ.");
                await shows.SaveAsync(new Showtime { Id = number, MovieId = I("movie"), ScreeningRoomId = I("room"), StartsAtUtc = VietnamTime.Utc(local), StandardPrice = D("price"), VipPrice = D("vip"), IsActive = active }); break;
            case "users":
                await userService.SaveAdminAsync(id, V("name", 100), V("email"), V("role"), int.TryParse(V("cluster"), out var clusterId) ? clusterId : null, active, V("password"), actorId); break;
            default: throw new BusinessException("Không hỗ trợ cập nhật mục này.");
        }
        db.TransactionLogs.Add(new TransactionLog { Event = "ADMIN_UPDATE", Message = $"{actorId}: cập nhật {section} #{id ?? "mới"}.", CreatedAtUtc = clock.GetUtcNow().UtcDateTime });
        await db.SaveChangesAsync();
    }
    private static string On(bool value) => value ? "Đang hoạt động" : "Ngừng hoạt động";
}
