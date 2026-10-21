using CinemaBooking.DataAccess;
using CinemaBooking.Domain;
using Microsoft.EntityFrameworkCore;

namespace CinemaBooking.Business.Services;

public class PaymentService(VegaDbContext db, BookingService bookings, IPaymentGateway gateway, TimeProvider clock)
{
    public async Task<GatewayRequest> PrepareAsync(Guid bookingId, string userId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var booking = await bookings.LockAsync(bookingId);
        if (booking is null || booking.UserId != userId) throw new BusinessException("Không tìm thấy đơn đặt vé của bạn.");
        if (booking.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime) throw new BusinessException("Đã hết thời gian giữ ghế.");
        if (booking.Status is not (BookingStatus.SEAT_HELD or BookingStatus.PAYMENT_PENDING)) throw new BusinessException("Đơn không ở trạng thái chờ thanh toán.");
        var payment = await db.Payments.SingleOrDefaultAsync(x => x.BookingId == bookingId && x.Status == PaymentStatus.PENDING);
        if (payment is null)
        {
            payment = new Payment { BookingId = bookingId, Amount = booking.Total, CreatedAtUtc = clock.GetUtcNow().UtcDateTime };
            db.Payments.Add(payment);
        }
        booking.Status = BookingStatus.PAYMENT_PENDING;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new GatewayRequest(payment.Id, bookingId, payment.TransactionCode, payment.Amount);
    }

    public async Task<BookingStatus> PayAsync(Guid bookingId, string userId, string scenario)
    {
        if (scenario is not ("SUCCESS" or "FAILED" or "TIMEOUT")) throw new BusinessException("Kết quả mô phỏng không hợp lệ.");
        var request = await PrepareAsync(bookingId, userId);
        var receipt = await gateway.PayAsync(request, scenario);
        return await ConfirmAsync(receipt);
    }

    public async Task<BookingStatus> ConfirmAsync(GatewayReceipt receipt)
    {
        if (!gateway.Verify(receipt)) throw new BusinessException("Chữ ký xác nhận thanh toán không hợp lệ.");
        db.ChangeTracker.Clear();
        var evidence = System.Text.Json.JsonSerializer.Serialize(receipt);
        // Commit the signed evidence independently so a failed issuance can be retried.
        await db.Payments.Where(x => x.Id == receipt.PaymentId && x.BookingId == receipt.BookingId && x.Amount == receipt.Amount
            && x.TransactionCode == receipt.TransactionCode && x.GatewayReceiptJson == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.GatewayReceiptJson, evidence).SetProperty(x => x.ReceiptReceivedAtUtc, (DateTime?)clock.GetUtcNow().UtcDateTime));
        var savedEvidence = await db.Payments.AsNoTracking().Where(x => x.Id == receipt.PaymentId).Select(x => x.GatewayReceiptJson).SingleOrDefaultAsync();
        if (savedEvidence != evidence) throw new BusinessException("Kết quả thanh toán không khớp giao dịch hoặc xác nhận đã lưu.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var booking = await bookings.LockAsync(receipt.BookingId) ?? throw new BusinessException("Không tìm thấy đơn.");
        var payment = await db.Payments.SingleOrDefaultAsync(x => x.Id == receipt.PaymentId && x.BookingId == booking.Id);
        if (payment is null || payment.Amount != receipt.Amount || payment.TransactionCode != receipt.TransactionCode || booking.Total != receipt.Amount)
            throw new BusinessException("Thông tin giao dịch thanh toán không khớp.");
        if (payment.Status != PaymentStatus.PENDING) { await tx.CommitAsync(); return booking.Status; }
        if (receipt.Status is not (PaymentStatus.SUCCESS or PaymentStatus.FAILED or PaymentStatus.TIMEOUT)) throw new BusinessException("Kết quả thanh toán không hợp lệ.");
        var now = clock.GetUtcNow().UtcDateTime;
        payment.ConfirmedAtUtc = now;
        var details = await db.BookingDetails.Where(x => x.BookingId == booking.Id).OrderBy(x => x.ShowtimeSeatId).ToListAsync();
        var owned = await db.ShowtimeSeats.CountAsync(x => x.HoldBookingId == booking.Id && x.Status == SeatStatus.HELD && x.HoldExpiresAtUtc > now);
        if (booking.Status != BookingStatus.PAYMENT_PENDING || booking.ExpiresAtUtc <= now || owned != details.Count || details.Count == 0)
        {
            payment.Status = receipt.Status == PaymentStatus.SUCCESS ? PaymentStatus.REFUND_REQUIRED : receipt.Status;
            if (booking.Status is BookingStatus.SEAT_HELD or BookingStatus.PAYMENT_PENDING) await bookings.ReleaseAsync(booking, BookingStatus.EXPIRED);
            bookings.Log(booking.Id, "LATE_PAYMENT", "Kết quả đến khi đơn không còn quyền giữ ghế; cần đối soát nếu đã thu tiền.");
        }
        else if (receipt.Status != PaymentStatus.SUCCESS)
        {
            payment.Status = receipt.Status;
            await bookings.ReleaseAsync(booking, receipt.Status == PaymentStatus.TIMEOUT ? BookingStatus.EXPIRED : BookingStatus.CANCELLED);
            bookings.Log(booking.Id, "PAYMENT_" + receipt.Status, "Không phát hành vé; đã giải phóng ghế.");
        }
        else
        {
            // Conditional updates protect against a seat reclaimed after its expiry while this callback waits.
            foreach (var detail in details)
            {
                var updated = await db.ShowtimeSeats.Where(x => x.Id == detail.ShowtimeSeatId && x.HoldBookingId == booking.Id && x.Status == SeatStatus.HELD && x.HoldExpiresAtUtc > now)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SeatStatus.SOLD).SetProperty(x => x.HoldExpiresAtUtc, (DateTime?)null));
                if (updated != 1) throw new BusinessException("Quyền giữ ghế đã thay đổi. Vui lòng đối soát giao dịch.");
            }
            payment.Status = PaymentStatus.SUCCESS;
            booking.Status = BookingStatus.PAID;
            await db.SaveChangesAsync();
            foreach (var detail in details) db.Tickets.Add(new Ticket { BookingDetailId = detail.Id, IssuedAtUtc = now });
            booking.Status = BookingStatus.TICKET_ISSUED;
            bookings.Log(booking.Id, "TICKET_ISSUED", $"Đã xác nhận giao dịch và phát hành {details.Count} vé.");
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return booking.Status;
    }

    public async Task<BookingStatus> RetryAsync(Guid paymentId)
    {
        var evidence = await db.Payments.AsNoTracking().Where(x => x.Id == paymentId).Select(x => x.GatewayReceiptJson).SingleOrDefaultAsync();
        if (evidence is null) throw new BusinessException("Chưa có xác nhận được bảo vệ từ cổng thanh toán.");
        var receipt = System.Text.Json.JsonSerializer.Deserialize<GatewayReceipt>(evidence) ?? throw new BusinessException("Xác nhận đã lưu không hợp lệ.");
        return await ConfirmAsync(receipt);
    }
}
