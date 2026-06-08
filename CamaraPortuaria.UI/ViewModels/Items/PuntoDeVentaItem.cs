using PuertoBB.Core.Entities.CamaraPortuaria;

namespace CamaraPortuaria.UI.ViewModels.Items;

/// <summary>Proyección de un punto de venta para la grilla de Configuración.</summary>
public class PuntoDeVentaItem
{
    public int     Id                  { get; init; }
    public string  Nombre              { get; init; } = string.Empty;
    public int     Numero              { get; init; }
    public bool    UsarHomologacion    { get; init; }
    public string? CertificadoRuta     { get; init; }
    public string? CertificadoPassword { get; init; }
    public string? CertificadoKeyRuta  { get; init; }
    public bool    Activo              { get; init; }

    public string Ambiente    => UsarHomologacion ? "Homologación" : "Producción";
    public string ActivoTexto => Activo ? "● Activo" : string.Empty;

    public static PuntoDeVentaItem From(PuntoDeVenta p) => new()
    {
        Id = p.Id,
        Nombre = p.Nombre,
        Numero = p.Numero,
        UsarHomologacion = p.UsarHomologacion,
        CertificadoRuta = p.CertificadoRuta,
        CertificadoPassword = p.CertificadoPassword,
        CertificadoKeyRuta = p.CertificadoKeyRuta,
        Activo = p.Activo
    };
}
