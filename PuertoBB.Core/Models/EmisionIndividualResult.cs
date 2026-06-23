namespace PuertoBB.Core.Models;

/// <summary>Resultado del diálogo de emisión individual. Null si el usuario canceló.</summary>
public record EmisionIndividualResult(
    int ClienteId,
    DateTime FechaEmision,
    List<ReciboLineaInput> Lineas,
    bool EnviarMail);
