namespace PuertoBB.Core.Afip;

/// <summary>Clase fiscal del comprobante (define la letra A/B/C según el régimen del emisor).</summary>
public enum ClaseFiscal { A, B, C }

/// <summary>Un tipo de comprobante AFIP seleccionable (código oficial + descripción + clase).</summary>
public sealed record ComprobanteAfipTipo(int Codigo, string Descripcion, ClaseFiscal Clase)
{
    /// <summary>Texto para mostrar en el combo: "15 — Recibo C".</summary>
    public string Display => $"{Codigo} — {Descripcion}";
}

/// <summary>
/// Catálogo de tipos de comprobante AFIP (tabla oficial <c>FEParamGetTiposCbte</c>).
/// Única fuente de la tabla AFIP: la UI no hardcodea códigos.
/// </summary>
public static class CatalogoComprobantesAfip
{
    /// <summary>
    /// Comprobantes principales seleccionables como "comprobante a emitir".
    /// Recibos primero (uso actual), luego Facturas (futuro).
    /// </summary>
    public static IReadOnlyList<ComprobanteAfipTipo> Principales { get; } =
    [
        new(15, "Recibo C",  ClaseFiscal.C),
        new(9,  "Recibo B",  ClaseFiscal.B),
        new(4,  "Recibo A",  ClaseFiscal.A),
        new(11, "Factura C", ClaseFiscal.C),
        new(6,  "Factura B", ClaseFiscal.B),
        new(1,  "Factura A", ClaseFiscal.A),
    ];

    /// <summary>Código AFIP de la Nota de Crédito por clase fiscal (A→3, B→8, C→13).</summary>
    private static readonly Dictionary<ClaseFiscal, int> NotaCreditoPorClase = new()
    {
        [ClaseFiscal.A] = 3,
        [ClaseFiscal.B] = 8,
        [ClaseFiscal.C] = 13,
    };

    /// <summary>Busca un comprobante principal por su código AFIP. Null si no está en el catálogo.</summary>
    public static ComprobanteAfipTipo? PorCodigo(int codigo) =>
        Principales.FirstOrDefault(c => c.Codigo == codigo);

    /// <summary>
    /// Código AFIP de la Nota de Crédito que corresponde al comprobante principal indicado
    /// (deriva de su clase fiscal). Si el código no está en el catálogo, devuelve
    /// <paramref name="codigoPrincipal"/> sin cambio (fallback defensivo).
    /// </summary>
    public static int NotaCreditoPara(int codigoPrincipal)
    {
        var principal = PorCodigo(codigoPrincipal);
        return principal is null ? codigoPrincipal : NotaCreditoPorClase[principal.Clase];
    }

    /// <summary>Descripción de la Nota de Crédito derivada del comprobante principal: "Nota de Crédito C".</summary>
    public static string DescripcionNotaCredito(int codigoPrincipal)
    {
        var principal = PorCodigo(codigoPrincipal);
        return principal is null ? "Nota de Crédito" : $"Nota de Crédito {principal.Clase}";
    }
}
