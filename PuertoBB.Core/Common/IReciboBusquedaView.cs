namespace PuertoBB.Core.Common;

/// <summary>
/// Vista de un recibo para la búsqueda de texto de la sección "Control": agrega a
/// <see cref="IReciboEstadoView"/> los campos que se muestran en la grilla, para poder armar el
/// texto buscable tal como se ve (agencia/empresa, comprobante, período, importe, fechas).
/// La implementan las entidades Recibo de CP y CM, que ya tienen todos estos campos.
/// </summary>
public interface IReciboBusquedaView : IReciboEstadoView
{
    string   ReceptorNombre    { get; }
    int      PeriodoAnio       { get; }
    int      PeriodoMes        { get; }
    decimal  Importe           { get; }
    int      PuntoDeVenta      { get; }
    long     NumeroComprobante { get; }
    DateTime FechaEmision      { get; }
}
