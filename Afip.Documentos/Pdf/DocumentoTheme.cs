using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Afip.Documentos.Pdf;

internal record DocumentoTheme
{
    public required string AcentoHex { get; init; }
    public string Fuente { get; init; } = Fonts.Calibri;

    public Color Acento          => Color.FromHex(AcentoHex);
    public Color Texto           => Color.FromHex("#1A1A1A");
    public Color TextoSecundario => Color.FromHex("#6B6B6B");
    public Color Borde           => Color.FromHex("#E0E0E0");

    public static DocumentoTheme From(EmisorDocumento emisor) =>
        new() { AcentoHex = emisor.ColorAcentoHex };
}
