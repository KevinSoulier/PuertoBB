using Afip.Abstractions;
using Afip.Wsaa;
using Afip.Wsfe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Afip.Mock;

/// <summary>Registro del stack mock de Afip.Net: ejerce el mapper, WsfeService y caché de tickets sin red ni certificado.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra el stack completo de Afip.Net con clientes mock (sin red, sin certificado).
    /// WsfeService, WsfeMapper y TicketCache corren igual que en producción; solo los clientes
    /// SOAP y el proveedor de tickets son reemplazados por implementaciones en memoria.
    /// </summary>
    public static IServiceCollection AddAfipMock(this IServiceCollection services)
    {
        services.AddSingleton<IWsaaClient, MockWsaaClient>();
        services.AddSingleton<IWsfeClient, MockWsfeClient>();
        services.TryAddSingleton<ITicketStore, InMemoryTicketStore>();
        services.AddSingleton<TicketCache>();
        services.AddSingleton<ITicketProvider, MockTicketProvider>();
        services.AddSingleton<IWsfeService, WsfeService>();
        return services;
    }
}
