using Afip.Documentos.Pdf;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;

namespace Afip.Documentos;

public static class DependencyInjection
{
    public static IServiceCollection AddAfipDocumentos(this IServiceCollection services)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        services.AddSingleton<IAfipDocumentosService, AfipDocumentosService>();
        return services;
    }
}
