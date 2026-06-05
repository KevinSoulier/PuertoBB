using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PuertoBB.Services.Pdf;

/// <summary>
/// Tema visual de los PDFs. Las fuentes embebidas SÍ están permitidas en PDF
/// (la regla "sin fuentes externas" aplica solo a XAML/WPF — ver ux-reglas.md).
/// </summary>
public record PdfTheme
{
    public required string AcentoHex { get; init; }
    public string Fuente { get; init; } = Fonts.Calibri;

    public Color Acento => Color.FromHex(AcentoHex);
    public Color Texto => Color.FromHex("#1A1A1A");
    public Color TextoSecundario => Color.FromHex("#6B6B6B");
    public Color Borde => Color.FromHex("#E0E0E0");

    public static PdfTheme CamaraPortuaria => new() { AcentoHex = "#1565C0" };
    public static PdfTheme CentroMaritimo  => new() { AcentoHex = "#00695C" };
}
