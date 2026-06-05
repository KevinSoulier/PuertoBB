using Microsoft.Extensions.DependencyInjection;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Services.Afip;
using PuertoBB.Services.Mail;
using PuertoBB.Services.Negocio;
using PuertoBB.Services.Pdf;
using QuestPDF.Infrastructure;

namespace PuertoBB.Services;

/// <summary>Registro de los servicios de la capa Services en el contenedor DI.</summary>
public static class DependencyInjection
{
    /// <summary>Servicios compartidos (PDF de ambas apps, QuestPDF license).</summary>
    public static IServiceCollection AddPuertoBBPdf(this IServiceCollection services)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        services.AddSingleton<ICamaraPortuariaPdfService, CamaraPortuariaPdfService>();
        services.AddSingleton<ICentroMaritimoPdfService, CentroMaritimoPdfService>();
        return services;
    }

    /// <summary>
    /// AFIP: real (WSAA/WSFE) si usarFake=false, o FakeAfipService para desarrollo/testing.
    /// Cuando se usa el real, además hay que registrar IWsaaClient/IWsfeClient (clientes SOAP).
    /// </summary>
    public static IServiceCollection AddPuertoBBAfip(this IServiceCollection services, bool usarFake)
    {
        if (usarFake)
        {
            services.AddSingleton<IAfipService, FakeAfipService>();
        }
        else
        {
            services.AddSingleton<WsaaTokenCache>(); // caché del ticket compartido entre llamadas
            services.AddTransient<Afip.Abstractions.IWsaaClient, Afip.Soap.WsaaSoapClient>();
            services.AddTransient<Afip.Abstractions.IWsfeClient, Afip.Soap.WsfeSoapClient>();
            services.AddTransient<IAfipService, AfipService>();
        }
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
