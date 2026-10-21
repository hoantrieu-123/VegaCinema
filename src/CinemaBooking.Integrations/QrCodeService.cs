using CinemaBooking.Domain;
using QRCoder;
namespace CinemaBooking.Integrations;
public class QrCodeService : IQrCode
{
    public byte[] CreatePng(string value)
    {
        using var data = QRCodeGenerator.GenerateQrCode(value, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        return qr.GetGraphic(8);
    }
}
