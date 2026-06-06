using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CentroMaritimo;

/// <summary>Configuración singleton (Id = 1) del Centro Marítimo.</summary>
public class Configuracion : BaseEntity
{
    public string RazonSocial  { get; set; } = string.Empty;
    public string Cuit         { get; set; } = string.Empty;
    public int    PuntoDeVenta { get; set; }

    // Tipos AFIP (configurables; default = Exento IVA)
    public int CodigoAfipRecibo        { get; set; } = 11;
    public int CodigoAfipNotaDeCredito { get; set; } = 13;

    // Certificado AFIP/WSAA
    public string? AfipCertificadoRuta     { get; set; }
    public string? AfipCertificadoPassword { get; set; }
    public bool    AfipUsarHomologacion    { get; set; } = false;

    // Apoderado fiscal
    public bool    UsarApoderado   { get; set; }
    public string? NombreApoderado { get; set; }
    public string? CuitApoderado   { get; set; }

    // Vouchers
    public decimal ImporteVoucherPredeterminado { get; set; } = 0;

    // Control de pagos
    public int DiasVencimiento { get; set; } = 30;

    // Mail saliente
    public string? SmtpHost       { get; set; }
    public int     SmtpPort       { get; set; }
    public string? SmtpUsuario    { get; set; }
    public string? SmtpPassword   { get; set; }
    public string? EmailRemitente { get; set; }
}
