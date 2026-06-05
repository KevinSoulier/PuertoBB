using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CamaraPortuaria;

/// <summary>Configuración singleton (Id = 1) de la Cámara Portuaria.</summary>
public class Configuracion : BaseEntity
{
    public string RazonSocial  { get; set; } = string.Empty;
    public string Cuit         { get; set; } = string.Empty;
    public int    PuntoDeVenta { get; set; }

    // Tipos AFIP (configurables; default = Exento IVA)
    public int CodigoAfipRecibo        { get; set; } = 11; // Recibo C
    public int CodigoAfipNotaDeCredito { get; set; } = 13; // Nota de Crédito C

    // Certificado AFIP/WSAA
    public string? AfipCertificadoRuta     { get; set; } // ruta al archivo .p12
    public string? AfipCertificadoPassword { get; set; } // contraseña del .p12
    public bool    AfipUsarHomologacion    { get; set; } = false; // solo desarrollo/testing

    // Control de pagos
    public int DiasVencimiento { get; set; } = 30;

    // Mail saliente
    public string? SmtpHost       { get; set; }
    public int     SmtpPort       { get; set; }
    public string? SmtpUsuario    { get; set; }
    public string? SmtpPassword   { get; set; } // texto plano; aceptable para app unipersonal
    public string? EmailRemitente { get; set; }
}
