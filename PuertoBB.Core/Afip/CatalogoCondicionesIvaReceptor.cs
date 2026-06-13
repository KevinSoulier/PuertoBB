namespace PuertoBB.Core.Afip;

/// <summary>Una condición frente al IVA del receptor (código oficial AFIP + descripción).</summary>
public sealed record CondicionIvaReceptor(int Codigo, string Descripcion)
{
    /// <summary>Texto para mostrar en el combo: "1 — IVA Responsable Inscripto".</summary>
    public string Display => $"{Codigo} — {Descripcion}";
}

/// <summary>
/// Catálogo de condiciones frente al IVA del receptor (tabla oficial <c>FEParamGetCondicionIvaReceptor</c>).
/// Obligatorio informarla al solicitar CAE desde la RG 5616 (error 10242 si falta).
/// Única fuente de la tabla AFIP: la UI no hardcodea códigos ni textos.
/// </summary>
public static class CatalogoCondicionesIvaReceptor
{
    public static IReadOnlyList<CondicionIvaReceptor> Todas { get; } =
    [
        new(1,  "IVA Responsable Inscripto"),
        new(4,  "IVA Sujeto Exento"),
        new(5,  "Consumidor Final"),
        new(6,  "Responsable Monotributo"),
        new(7,  "Sujeto No Categorizado"),
        new(8,  "Proveedor del Exterior"),
        new(9,  "Cliente del Exterior"),
        new(10, "IVA Liberado – Ley N° 19.640"),
        new(13, "Monotributista Social"),
        new(15, "IVA No Alcanzado"),
        new(16, "Monotributo Trabajador Independiente Promovido"),
    ];

    /// <summary>Busca una condición por su código AFIP. Null si no está en el catálogo.</summary>
    public static CondicionIvaReceptor? PorCodigo(int codigo) =>
        Todas.FirstOrDefault(c => c.Codigo == codigo);

    /// <summary>Descripción de la condición, para snapshots y PDF. Null si el código es null o desconocido.</summary>
    public static string? Descripcion(int? codigo) =>
        codigo is int c ? PorCodigo(c)?.Descripcion : null;
}
