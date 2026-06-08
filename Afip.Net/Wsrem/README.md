# Wsrem/ — Remito Electrónico (punto de extensión, no implementado)

Esta carpeta está reservada para integrar a futuro el **Remito Electrónico** de AFIP/ARCA, que es un
**web service distinto a WSFE** (los remitos NO se piden con `FECAESolicitar`). Según el régimen, el
servicio es sectorial:

| Régimen | Servicio WSAA | WSDL homologación (ejemplo) |
|---|---|---|
| Remito Cárnico | `wsremcarne` | `https://fwshomo.afip.gov.ar/wsremcarne/RemCarneService?wsdl` |
| Remito Harina | `wsremharina` | `https://fwshomo.afip.gov.ar/wsremharina/RemHarinaService?wsdl` |
| Remito Azúcar/Derivados | `wsremazucar` | `https://fwshomo.afip.gov.ar/wsremazucar/RemAzucarService?wsdl` |

> El negocio portuario probablemente **no** encaje en ninguno de los regímenes sectoriales actuales;
> antes de implementar, validar con `/investigador-afip` qué servicio corresponde (o si aplica el
> Remito Electrónico genérico vigente al momento).

## Cómo agregarlo (la autenticación ya está resuelta y se reutiliza)

1. **Generar el cliente SOAP** desde el WSDL con `dotnet-svcutil`:
   ```bash
   cd Afip.Net
   dotnet-svcutil "<WSDL del wsrem*>" -n "*,Afip.Soap.Wsrem" -o Wsrem/Soap/Generated/WsremReference.cs
   ```
2. **Crear el cliente low-level** `Wsrem/Soap/WsremSoapClient.cs` (análogo a `WsfeSoapClient`) y su
   interfaz `IWsremClient` en `Abstractions/`.
3. **Crear la fachada** `Wsrem/IWsremService` + `WsremService`, que obtiene el TA reutilizando el
   `ITicketProvider` ya existente:
   ```csharp
   var ticket = await _ticketProvider.GetTicketAsync("wsremcarne", options, ct);
   ```
   El `TicketCache` ya está keyed por `(CUIT, servicio)`, así que el TA de remitos no pisa al de wsfe.
4. **Registrar** los nuevos tipos en `DependencyInjection.AddAfip()`.
5. **Modelos** propios del remito en `Wsrem/Models/` (no reutilizar los de WSFE).

No se requiere tocar WSAA: la firma del TRA, el login y el cacheo ya son genéricos por servicio.
