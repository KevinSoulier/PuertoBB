using QRCoder;

namespace Afip.Documentos.Qr;

public static class AfipQrBuilder
{
    /// <summary>Genera un PNG del QR de AFIP sin depender de System.Drawing (usa QRCoder.PngByteQRCode).</summary>
    public static byte[] GenerarPng(AfipQrPayload payload)
    {
        var url = payload.BuildUrl();
        using var gen = new QRCodeGenerator();
        var data = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(4);   // 4 px por módulo → ~150×150 px para texto mediano
    }
}
