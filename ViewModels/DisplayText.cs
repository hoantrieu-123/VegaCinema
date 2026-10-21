namespace VegaCinema.ViewModels;
public static class DisplayText
{
    public static string T(object? value) => value?.ToString() switch
    {
        "CREATED" => "Đã tạo", "SEAT_HELD" => "Đang giữ ghế", "PAYMENT_PENDING" => "Chờ thanh toán",
        "PAID" => "Đã thanh toán", "TICKET_ISSUED" => "Đã phát hành vé", "CANCELLED" => "Đã hủy", "EXPIRED" => "Đã hết hạn",
        "AVAILABLE" => "Còn trống", "HELD" => "Đang giữ", "SOLD" => "Đã bán", "SELECTED" => "Đang chọn",
        "PENDING" => "Đang xử lý", "SUCCESS" => "Thành công", "FAILED" => "Thất bại", "TIMEOUT" => "Hết thời gian",
        "REFUND_REQUIRED" => "Cần đối soát hoàn tiền", "REFUNDED" => "Đã hoàn tiền mô phỏng",
        "VALID" => "Còn hiệu lực", "USED" => "Đã sử dụng", "STANDARD" => "Ghế thường", "VIP" => "Ghế VIP",
        "NOW_SHOWING" => "Đang chiếu", "COMING_SOON" => "Sắp chiếu", "ARCHIVED" => "Ngừng phát hành",
        "CUSTOMER" => "Khách hàng", "CINEMA_STAFF" => "Nhân viên rạp", "TICKET_CHECKER" => "Nhân viên kiểm vé", "ADMIN" => "Quản trị viên", "CINEMA_MANAGER" => "Quản lý rạp",
        _ => value?.ToString() ?? ""
    };
}
