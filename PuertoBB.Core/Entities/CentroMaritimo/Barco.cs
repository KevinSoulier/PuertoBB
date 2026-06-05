using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CentroMaritimo;

/// <summary>Barco que ingresa al puerto. Índice único: (Nombre). Extensible (Bandera, IMO, etc.).</summary>
public class Barco : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
}
