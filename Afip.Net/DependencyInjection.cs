using Afip.Abstractions;
using Afip.Padron;
using Afip.Soap;
using Afip.Wsaa;
using Afip.Wsfe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Afip;

/// <summary>Registro del cliente AFIP (WSAA + WSFE + Padrón) en el contenedor DI.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra el cliente AFIP. Por defecto cachea el Ticket de Acceso en memoria.
    /// Para persistirlo en disco (recomendado en escritorio), registrá un
    /// <see cref="ITicketStore"/> antes de llamar a este método:
    /// <code>
    /// services.AddSingleton&lt;ITicketStore&gt;(new FileTicketStore(carpeta));
    /// services.AddAfip();
    /// </code>
    /// </summary>
    public static IServiceCollection AddAfip(this IServiceCollection services)
    {
        services.AddSingleton<IWsaaClient, WsaaSoapClient>();
        services.AddSingleton<IWsfeClient, WsfeSoapClient>();
        services.TryAddSingleton<ITicketStore, InMemoryTicketStore>();
        services.AddSingleton<TicketCache>();
        services.AddSingleton<ITicketProvider, TicketProvider>();
        services.AddSingleton<IWsfeService, WsfeService>();
        services.AddSingleton<IPadronClient, PadronSoapClient>();
        services.AddSingleton<IPadronService, PadronService>();
        return services;
    }
}
