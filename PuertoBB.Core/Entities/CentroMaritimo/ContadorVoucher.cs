using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CentroMaritimo;

/// <summary>
/// Contador global de numeración de vouchers (singleton, Id = 1).
/// Editable: permite fijar el valor inicial al migrar del sistema manual.
/// Secuencia global única — no hay series con letra prefija.
/// </summary>
public class ContadorVoucher : BaseEntity
{
    public int UltimoNumero { get; set; }
}
