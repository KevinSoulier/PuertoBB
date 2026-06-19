using System.Net;
using System.Text.RegularExpressions;

namespace PuertoBB.Core.Mail;

/// <summary>
/// Plantilla del mail con que se envían los comprobantes. Lógica pura (sin WPF): el cuerpo enriquecido
/// se convierte a HTML en la capa UI y acá solo se reemplazan las variables <c>{clave}</c> por sus valores.
/// </summary>
public static class PlantillaMail
{
    /// <summary>Asunto por defecto. Usa <c>{comprobante}</c> para distinguir Recibo / Nota de crédito.</summary>
    public const string DefaultAsunto = "{comprobante} {periodo} — {razonSocial}";

    /// <summary>Cuerpo por defecto (texto plano). Equivale a las frases hardcodeadas históricas.</summary>
    public const string DefaultCuerpoTexto =
        "Estimados,\n\nAdjuntamos el comprobante correspondiente al período {periodo}.\n\nSaludos.";

    /// <summary>Variables disponibles (token + descripción) para mostrarlas en la UI de Configuración.</summary>
    public static readonly IReadOnlyList<(string Token, string Descripcion)> Variables = new[]
    {
        ("{periodo}",     "Período del comprobante (ej. «junio 2026»)"),
        ("{receptor}",    "Nombre del destinatario"),
        ("{razonSocial}", "Razón social del emisor"),
        ("{comprobante}", "Tipo de comprobante (Recibo, Nota de crédito…)"),
        ("{numero}",      "Número del comprobante (ej. «0001-00000123»)"),
        ("{importe}",     "Importe total"),
    };

    /// <summary>
    /// Reemplaza en <paramref name="plantilla"/> cada <c>{clave}</c> presente en <paramref name="valores"/>
    /// por su valor (la coincidencia del nombre es insensible a mayúsculas). Las variables no incluidas en
    /// el diccionario se dejan intactas. Devuelve cadena vacía si la plantilla es nula o vacía.
    /// </summary>
    public static string Aplicar(string? plantilla, IReadOnlyDictionary<string, string?> valores)
    {
        if (string.IsNullOrEmpty(plantilla)) return string.Empty;

        var resultado = plantilla;
        foreach (var (clave, valor) in valores)
            resultado = resultado.Replace("{" + clave + "}", valor ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        return resultado;
    }

    /// <summary>Versión en texto plano de un cuerpo HTML (la alternativa del mail): quita las etiquetas,
    /// decodifica las entidades y normaliza los espacios.</summary>
    public static string QuitarHtml(string? html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        var sinTags = Regex.Replace(html, "<[^>]+>", " ");
        return Regex.Replace(WebUtility.HtmlDecode(sinTags), @"\s+", " ").Trim();
    }
}
