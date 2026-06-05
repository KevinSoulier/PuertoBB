using PuertoBB.Core.Entities.Common;

namespace PuertoBB.Core.Entities.CentroMaritimo;

/// <summary>Voucher de ingreso de un barco. Índice único: (Numero).</summary>
public class Voucher : BaseEntity
{
    public int     AgenciaId { get; set; }
    public Agencia Agencia   { get; set; } = null!;

    public int   BarcoId { get; set; }
    public Barco Barco   { get; set; } = null!;

    public int      Numero  { get; set; } // auto-generado desde ContadorVoucher; editable para importar histórico
    public decimal  Importe { get; set; } // monto recibido
    public DateTime Fecha   { get; set; } // fecha de entrada del barco

    public int PeriodoAnio { get; set; } // derivado de Fecha al guardar
    public int PeriodoMes  { get; set; }

    public int?    ReciboId { get; set; } // null = pendiente de consolidar
    public Recibo? Recibo   { get; set; }
}
