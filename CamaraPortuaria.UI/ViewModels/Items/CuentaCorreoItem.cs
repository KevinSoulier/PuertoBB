using PuertoBB.Core.Entities.CamaraPortuaria;

namespace CamaraPortuaria.UI.ViewModels.Items;

/// <summary>Proyección de una cuenta de correo para la grilla de Configuración.</summary>
public class CuentaCorreoItem
{
    public int     Id             { get; init; }
    public string  Nombre         { get; init; } = string.Empty;
    public string? EmailRemitente { get; init; }
    public int     Autenticacion  { get; init; }
    public bool    Activo         { get; init; }

    public string ModoTexto => Autenticacion switch { 0 => "Sin auth", 2 => "OAuth2", _ => "Básica" };
    public string ActivoTexto => Activo ? "● Activa" : string.Empty;

    public static CuentaCorreoItem From(CuentaCorreo c) => new()
    {
        Id = c.Id,
        Nombre = c.Nombre,
        EmailRemitente = c.EmailRemitente,
        Autenticacion = c.Autenticacion,
        Activo = c.Activo
    };
}
