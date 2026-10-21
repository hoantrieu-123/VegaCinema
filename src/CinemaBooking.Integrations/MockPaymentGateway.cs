using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CinemaBooking.Domain;

namespace CinemaBooking.Integrations;

public class MockPaymentGateway(string signingKey) : IPaymentGateway
{
    private readonly byte[] key = Encoding.UTF8.GetBytes(signingKey.Length >= 32 ? signingKey : throw new InvalidOperationException("Payment:SigningKey phải có ít nhất 32 ký tự."));
    public async Task<GatewayReceipt> PayAsync(GatewayRequest request, string scenario, CancellationToken cancellationToken = default)
    {
        await Task.Delay(200, cancellationToken);
        var status = scenario switch { "SUCCESS" => PaymentStatus.SUCCESS, "FAILED" => PaymentStatus.FAILED, "TIMEOUT" => PaymentStatus.TIMEOUT, _ => throw new BusinessException("Kịch bản không hợp lệ.") };
        var receipt = new GatewayReceipt(request.PaymentId, request.BookingId, request.TransactionCode, request.Amount, status, "");
        return receipt with { Signature = Sign(receipt) };
    }
    public bool Verify(GatewayReceipt receipt)
    {
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(receipt.Signature), Convert.FromHexString(Sign(receipt))); }
        catch (FormatException) { return false; }
    }
    private string Sign(GatewayReceipt r) => Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(
        $"{r.PaymentId:N}|{r.BookingId:N}|{r.TransactionCode}|{r.Amount.ToString("F2", CultureInfo.InvariantCulture)}|{r.Status}")));
}
