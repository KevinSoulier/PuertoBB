using Afip.Abstractions;
using Afip.Soap.Padron;

namespace Afip.Soap;

/// <summary>
/// Mapea la respuesta de getPersona_v2 (constancia de inscripción) al modelo neutro.
/// Público para testear el mapeo y la derivación de condición IVA sin invocar al webservice.
/// </summary>
public static class PadronMapper
{
    private const int ImpuestoIva = 30;        // IVA (régimen general → Responsable Inscripto)
    private const int ImpuestoIvaExento = 32;  // IVA EXENTO

    /// <summary>Null solo si la respuesta no trae ningún dato de la persona.</summary>
    public static PadronPersona? ToPersona(personaReturn? resp)
    {
        if (resp is null) return null;
        var dg = resp.datosGenerales;
        var ec = resp.errorConstancia;
        if (dg is null && ec is null) return null;

        return new PadronPersona
        {
            RazonSocial = NombreCompleto(dg) ?? NombreCompleto(ec),
            Domicilio = FormatearDomicilio(dg?.domicilioFiscal),
            EsPersonaJuridica = string.Equals(dg?.tipoPersona, "JURIDICA", StringComparison.OrdinalIgnoreCase),
            // Con errorConstancia no hay datos impositivos confiables: no sugerir condición.
            CondicionIvaSugeridaId = ec is null ? DerivarCondicionIva(resp) : null,
            Observaciones = ec?.error?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList() ?? []
        };
    }

    /// <summary>Derivación RG 5616: monotributo→6, impuesto 30 (IVA)→1, impuesto 32 (exento)→4, ninguno→15.</summary>
    private static int DerivarCondicionIva(personaReturn resp)
    {
        if (resp.datosMonotributo is not null) return 6;

        var impuestos = (resp.datosRegimenGeneral?.impuesto ?? [])
            .Where(i => i.estadoImpuesto is null || i.estadoImpuesto.StartsWith("AC", StringComparison.OrdinalIgnoreCase))
            .Select(i => i.idImpuesto)
            .ToHashSet();

        if (impuestos.Contains(ImpuestoIva)) return 1;
        if (impuestos.Contains(ImpuestoIvaExento)) return 4;
        return 15;
    }

    private static string? NombreCompleto(datosGenerales? dg)
        => dg is null ? null
            : !string.IsNullOrWhiteSpace(dg.razonSocial) ? dg.razonSocial
            : Juntar(dg.apellido, dg.nombre);

    private static string? NombreCompleto(errorConstancia? ec)
        => ec is null ? null : Juntar(ec.apellido, ec.nombre);

    private static string? Juntar(string? apellido, string? nombre)
    {
        var partes = new[] { apellido, nombre }.Where(p => !string.IsNullOrWhiteSpace(p));
        var texto = string.Join(" ", partes);
        return texto.Length > 0 ? texto : null;
    }

    private static string? FormatearDomicilio(domicilio? d)
    {
        if (d is null) return null;
        var partes = new[] { d.direccion, d.localidad, d.descripcionProvincia }
            .Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (partes.Count == 0) return null;
        var texto = string.Join(", ", partes);
        return string.IsNullOrWhiteSpace(d.codPostal) ? texto : $"{texto} (CP {d.codPostal})";
    }
}
