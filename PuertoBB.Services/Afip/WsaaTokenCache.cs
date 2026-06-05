namespace PuertoBB.Services.Afip;

/// <summary>
/// Caché thread-safe del Ticket de Acceso WSAA (válido ~12 hs). Renueva con margen de 10 minutos.
/// Patrón SemaphoreSlim + double-check (ver afip-integracion.md).
/// </summary>
public class WsaaTokenCache
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private string? _token;
    private string? _sign;
    private DateTime _expiration = DateTime.MinValue;

    public async Task<(string Token, string Sign)> GetValidTicketAsync(
        Func<Task<(string Token, string Sign, DateTime Expiration)>> renewFunc,
        CancellationToken ct = default)
    {
        if (_expiration > DateTime.Now.AddMinutes(10) && _token is not null)
            return (_token, _sign!);

        await _semaphore.WaitAsync(ct);
        try
        {
            if (_expiration > DateTime.Now.AddMinutes(10) && _token is not null)
                return (_token, _sign!);

            var (token, sign, expiration) = await renewFunc();
            (_token, _sign, _expiration) = (token, sign, expiration);
            return (token, sign);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
