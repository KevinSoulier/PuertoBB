using global::Afip;
using global::Afip.Documentos;
using global::Afip.Mock;
using global::Afip.Wsaa;
using Microsoft.Extensions.DependencyInjection;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Services.Afip;
using PuertoBB.Services.Mail;
using PuertoBB.Services.Negocio;
using PuertoBB.Services.Pdf;

namespace PuertoBB.Services;

/// <summary>Registro de los servicios de la capa Services en el contenedor DI.</summary>
public static class DependencyInjection
{
    /// <summary>Servicios PDF: genera comprobantes AFIP (Afip.Documentos) y los servicios de cada app.</summary>
    public static IServiceCollection AddPuertoBBPdf(this IServiceCollection services)
    {
        services.AddAfipDocumentos();   // fija QuestPDF License + registra IAfipDocumentosService
        services.AddSingleton<IPdfMerger, PdfMerger>();
        // Transient (no Singleton): dependen de IAfipConfigProvider que es Transient (arrastra DbContext)
        services.AddTransient<ICamaraPortuariaPdfService, CamaraPortuariaPdfService>();
        services.AddTransient<ICentroMaritimoPdfService, CentroMaritimoPdfService>();
        return services;
    }

    /// <summary>
    /// AFIP real (librería Afip.Net: WSAA/WSFE).
    /// <paramref name="ticketCacheDir"/> (opcional): carpeta donde persistir el Ticket de Acceso cifrado
    /// con DPAPI (sobrevive reinicios y evita re-loguear en WSAA). Si es null, el ticket se cachea en memoria.
    /// </summary>
    public static IServiceCollection AddPuertoBBAfip(this IServiceCollection services, string? ticketCacheDir = null)
    {
        if (!string.IsNullOrWhiteSpace(ticketCacheDir))
            services.AddSingleton<ITicketStore>(new FileTicketStore(ticketCacheDir));

        services.AddAfip();                                   // Afip.Net: WSAA + WSFE
        services.AddTransient<IAfipService, AfipService>();   // adaptador dominio ↔ librería
        return services;
    }

    /// <summary>
    /// AFIP mock: stack completo de Afip.Net (mapper, orquestación, caché de tickets) sin red ni
    /// certificado real. Usar cuando se quiere probar el flujo AFIP end-to-end pero sin conectarse.
    /// </summary>
    public static IServiceCollection AddPuertoBBAfipMock(this IServiceCollection services)
    {
        services.AddAfipMock();
        services.AddTransient<IAfipService, AfipService>();
        return services;
    }

    /// <summary>Mail: real (MailKit) o FakeMailService para desarrollo/testing.</summary>
    public static IServiceCollection AddPuertoBBMail(this IServiceCollection services, bool usarFake)
    {
        if (usarFake)
            services.AddSingleton<IMailService, FakeMailService>();
        else
            services.AddTransient<IMailService, MailService>();
        return services;
    }

    public static IServiceCollection AddCamaraPortuariaServices(this IServiceCollection services)
    {
        services.AddTransient<ICamaraPortuariaReciboService, CamaraPortuariaReciboService>();
        return services;
    }

    public static IServiceCollection AddCentroMaritimoServices(this IServiceCollection services)
    {
        services.AddTransient<ICentroMaritimoReciboService, CentroMaritimoReciboService>();
        services.AddTransient<IVoucherService, VoucherService>();
        return services;
    }
}
