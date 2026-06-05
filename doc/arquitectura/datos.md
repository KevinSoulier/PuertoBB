# Modelo de datos

Todas las entidades viven en `PuertoBB.Core/Entities/`. Cada aplicación tiene su propio namespace y DbContext. Los enums compartidos viven en `PuertoBB.Core/Enums/`.

---

## Cámara Portuaria — `Core/Entities/CamaraPortuaria/`

### `Empresa`
```csharp
public class Empresa : BaseEntity
{
    public string Nombre         { get; set; } = string.Empty;
    public string RazonSocial    { get; set; } = string.Empty;
    public string Cuit           { get; set; } = string.Empty;
    public string? Email         { get; set; }
    public string? Domicilio     { get; set; }
    public bool   Activa         { get; set; } = true;

    public ICollection<EmpresaGrupo> Grupos  { get; set; } = [];
    public ICollection<Recibo>       Recibos { get; set; } = [];
}
```

### `GrupoFacturacion`
```csharp
public class GrupoFacturacion : BaseEntity
{
    public string  Nombre       { get; set; } = string.Empty;
    public string? Descripcion  { get; set; }
    public decimal Importe      { get; set; }
    public bool    Activo       { get; set; } = true;

    public ICollection<EmpresaGrupo> Empresas { get; set; } = [];
    public ICollection<Recibo>       Recibos  { get; set; } = [];
}
```

### `EmpresaGrupo` *(join N:M)*
```csharp
public class EmpresaGrupo : BaseEntity
{
    public int              EmpresaId { get; set; }
    public Empresa          Empresa   { get; set; } = null!;
    public int              GrupoFacturacionId { get; set; }
    public GrupoFacturacion Grupo     { get; set; } = null!;
}
// Índice único: (EmpresaId, GrupoFacturacionId)
```

### `Recibo`
```csharp
public class Recibo : BaseEntity
{
    public int              EmpresaId          { get; set; }
    public Empresa          Empresa            { get; set; } = null!;
    public int?             GrupoFacturacionId { get; set; } // null = emisión individual
    public GrupoFacturacion? Grupo             { get; set; }

    public int     PeriodoAnio        { get; set; }
    public int     PeriodoMes         { get; set; }
    public decimal Importe            { get; set; }
    public string  Detalle            { get; set; } = string.Empty;

    // Comprobante AFIP
    public int             PuntoDeVenta         { get; set; }
    public TipoComprobante TipoComprobante       { get; set; }
    public long            NumeroComprobante     { get; set; }
    public string          CAE                   { get; set; } = string.Empty;
    public DateTime        FechaVencimientoCAE   { get; set; }
    public DateTime        FechaEmision          { get; set; }

    public ReciboEstado Estado { get; set; } = ReciboEstado.Emitido;

    // Control de pagos
    public DateTime  FechaVencimientoPago { get; set; }  // = FechaEmision + Configuracion.DiasVencimiento
    public DateTime? FechaPago            { get; set; }  // null hasta que Laura marque como pagado

    public NotaDeCredito? NotaDeCredito { get; set; } // null mientras no esté anulado
}
// Índice único: (EmpresaId, GrupoFacturacionId, PeriodoAnio, PeriodoMes) — bloqueo de duplicados
```

### `NotaDeCredito`
```csharp
public class NotaDeCredito : BaseEntity
{
    public int    ReciboOriginalId  { get; set; }
    public Recibo ReciboOriginal    { get; set; } = null!;

    // Comprobante AFIP (tipo nota de crédito)
    public int             PuntoDeVenta        { get; set; }
    public TipoComprobante TipoComprobante     { get; set; }
    public long            NumeroComprobante   { get; set; }
    public string          CAE                 { get; set; } = string.Empty;
    public DateTime        FechaVencimientoCAE { get; set; }
    public DateTime        FechaEmision        { get; set; }
}
```

### `Configuracion` *(singleton, Id = 1 siempre)*
```csharp
public class Configuracion : BaseEntity
{
    public string  RazonSocial   { get; set; } = string.Empty;
    public string  Cuit          { get; set; } = string.Empty;
    public int     PuntoDeVenta  { get; set; }

    // Control de pagos
    public int DiasVencimiento { get; set; } = 30;  // días desde emisión hasta considerar vencido

    // Mail saliente
    public string? SmtpHost       { get; set; }
    public int     SmtpPort       { get; set; }
    public string? SmtpUsuario    { get; set; }
    public string? SmtpPassword   { get; set; }
    public string? EmailRemitente { get; set; }
}
```

---

## Centro Marítimo — `Core/Entities/CentroMaritimo/`

### `Agencia`
```csharp
public class Agencia : BaseEntity
{
    public string  Nombre      { get; set; } = string.Empty;
    public string  RazonSocial { get; set; } = string.Empty;
    public string  Cuit        { get; set; } = string.Empty;
    public string? Email       { get; set; }
    public string? Domicilio   { get; set; }
    public bool    Activa      { get; set; } = true;

    public ICollection<AgenciaGrupo> Grupos   { get; set; } = [];
    public ICollection<Voucher>      Vouchers { get; set; } = [];
    public ICollection<Recibo>       Recibos  { get; set; } = [];
}
```

### `GrupoFacturacion`
```csharp
public class GrupoFacturacion : BaseEntity
{
    public string  Nombre      { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Importe     { get; set; }
    public bool    Activo      { get; set; } = true;

    public ICollection<AgenciaGrupo> Agencias { get; set; } = [];
}
```

### `AgenciaGrupo` *(join N:M)*
```csharp
public class AgenciaGrupo : BaseEntity
{
    public int              AgenciaId          { get; set; }
    public Agencia          Agencia            { get; set; } = null!;
    public int              GrupoFacturacionId { get; set; }
    public GrupoFacturacion Grupo              { get; set; } = null!;
}
// Índice único: (AgenciaId, GrupoFacturacionId)
```

### `Barco`
```csharp
public class Barco : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
    // Extensible: Bandera, TipoBarco, IMO, etc.
}
// Índice único: (Nombre) — evita duplicados por tipeo distinto
```
> **Decisión:** entidad separada para garantizar nombres consistentes entre vouchers y permitir historial de entradas por barco. Se puede buscar/autocompletar al cargar un voucher nuevo.

### `ContadorVoucher` *(singleton, controla el número siguiente)*
```csharp
public class ContadorVoucher : BaseEntity  // Id = 1 siempre
{
    public int UltimoNumero { get; set; }  // editable: permite fijar el valor inicial al migrar del sistema manual
}
```
> **⚠️ Pendiente de confirmar:** ¿los vouchers usan una sola secuencia numérica global (1, 2, 3…) o hay múltiples series con letra prefija (A-001, B-001…)? Si hay múltiples series, `ContadorVoucher` pasa a tener también un campo `Serie` con índice único por serie.

### `Voucher`
```csharp
public class Voucher : BaseEntity
{
    public int     AgenciaId { get; set; }
    public Agencia Agencia   { get; set; } = null!;

    public int   BarcoId { get; set; }
    public Barco Barco   { get; set; } = null!;

    public int     Numero  { get; set; }   // auto-generado a partir de ContadorVoucher; editable para importar histórico
    public decimal Importe { get; set; }   // monto recibido
    public DateTime Fecha  { get; set; }   // fecha de entrada del barco

    public int PeriodoAnio { get; set; }   // derivado de Fecha al guardar
    public int PeriodoMes  { get; set; }

    public int?    ReciboId { get; set; }  // null = pendiente de consolidar
    public Recibo? Recibo   { get; set; }
}
// Índice único: (Numero) — o (Serie, Numero) si se confirma sistema por series
```

### `Recibo`
```csharp
public class Recibo : BaseEntity
{
    public int               AgenciaId          { get; set; }
    public Agencia           Agencia            { get; set; } = null!;
    public int?              GrupoFacturacionId { get; set; } // null = consolidado de vouchers o emisión individual
    public GrupoFacturacion? Grupo              { get; set; }

    public int     PeriodoAnio { get; set; }
    public int     PeriodoMes  { get; set; }
    public decimal Importe     { get; set; }  // = SUM(Vouchers.Importe) cuando EsConsolidadoVouchers=true
    public string  Detalle     { get; set; } = string.Empty;
    // Cuando EsConsolidadoVouchers=true, Detalle se auto-genera:
    // "Vouchers Nros: 1234, 1235, 1236" (los números de todos los vouchers del período)

    public bool EsConsolidadoVouchers { get; set; }
    // true  → recibo surgido del cierre de período (agrupa vouchers)
    // false → cuota social (de grupo) o emisión individual

    // Apoderado fiscal (copiado de Configuracion al momento de emitir, para inmutabilidad del comprobante)
    public bool    EsApoderado      { get; set; }
    public string? NombreApoderado  { get; set; }
    public string? CuitApoderado    { get; set; }

    // Comprobante AFIP
    public int             PuntoDeVenta        { get; set; }
    public TipoComprobante TipoComprobante     { get; set; }
    public long            NumeroComprobante   { get; set; }
    public string          CAE                 { get; set; } = string.Empty;
    public DateTime        FechaVencimientoCAE { get; set; }
    public DateTime        FechaEmision        { get; set; }

    public ReciboEstado Estado { get; set; } = ReciboEstado.Emitido;

    // Control de pagos
    public DateTime  FechaVencimientoPago { get; set; }  // = FechaEmision + Configuracion.DiasVencimiento
    public DateTime? FechaPago            { get; set; }  // null hasta que Laura marque como pagado

    public ICollection<Voucher> Vouchers      { get; set; } = [];  // populado solo cuando EsConsolidadoVouchers=true
    public NotaDeCredito?       NotaDeCredito { get; set; }
}
// Índice único: (AgenciaId, GrupoFacturacionId, PeriodoAnio, PeriodoMes) — bloqueo de duplicados en emisión masiva
// Los recibos consolidados de vouchers son únicos por: (AgenciaId, PeriodoAnio, PeriodoMes) donde EsConsolidadoVouchers=true
```

> **Documentos generados en cierre de período (por agencia):**
> 1. PDFs individuales de cada voucher del período
> 2. PDF consolidado final = recibo AFIP + todos los vouchers del período concatenados
>
> Solo el PDF consolidado se envía por mail. Los PDFs de vouchers son generados como parte del documento consolidado.
> Los PDFs no se almacenan en la DB; se regeneran a demanda desde los datos del recibo y sus vouchers.

### `NotaDeCredito`
```csharp
public class NotaDeCredito : BaseEntity
{
    public int    ReciboOriginalId { get; set; }
    public Recibo ReciboOriginal   { get; set; } = null!;

    public int             PuntoDeVenta        { get; set; }
    public TipoComprobante TipoComprobante     { get; set; }
    public long            NumeroComprobante   { get; set; }
    public string          CAE                 { get; set; } = string.Empty;
    public DateTime        FechaVencimientoCAE { get; set; }
    public DateTime        FechaEmision        { get; set; }
}
```

### `Configuracion` *(singleton, Id = 1 siempre)*
```csharp
public class Configuracion : BaseEntity
{
    public string  RazonSocial   { get; set; } = string.Empty;
    public string  Cuit          { get; set; } = string.Empty;
    public int     PuntoDeVenta  { get; set; }

    // Control de pagos
    public int DiasVencimiento { get; set; } = 30;

    // Apoderado fiscal global (aplica a todos los recibos si está activo)
    public bool    UsarApoderado    { get; set; }
    public string? NombreApoderado  { get; set; }
    public string? CuitApoderado    { get; set; }

    // Mail saliente
    public string? SmtpHost       { get; set; }
    public int     SmtpPort       { get; set; }
    public string? SmtpUsuario    { get; set; }
    public string? SmtpPassword   { get; set; }
    public string? EmailRemitente { get; set; }
}
```

---

## Enums compartidos — `Core/Enums/`

```csharp
public enum ReciboEstado  { Emitido, Enviado, Pagado, Vencido, Anulado }
public enum TipoComprobante { Recibo, NotaDeCredito }  // ampliar con códigos AFIP reales
```

---

## Decisiones de diseño

| Decisión | Elección | Motivo |
| --- | --- | --- |
| Entidades separadas por app | Sí (`CamaraPortuaria.` vs `CentroMaritimo.`) | Cada app tiene DB propia; compartir entidades crearía acoplamiento entre contextos |
| Período como dos ints | `PeriodoAnio + PeriodoMes` | Evita ambigüedades de timezone con DateTime; indexa eficientemente |
| `Configuracion` singleton | Id = 1 por convención | Aplicación unipersonal sin multi-tenant; un solo set de config por DB |
| `EmpresaGrupo` como entidad separada | Sí | Permite agregar campos futuros (ej. fecha de alta en el grupo) sin migraciones complejas |
| `NotaDeCredito` separada de `Recibo` | Sí | Son comprobantes AFIP distintos (diferente tipo); relación explícita facilita auditoría |
| Apoderado en `Recibo` Centro Marítimo | Heredado de `Configuracion` al emitir | El apoderado no cambia recibo a recibo; se copia al momento de emisión para inmutabilidad del comprobante |

---

## Pendiente de confirmar

- [ ] Tipos AFIP exactos para Recibo y Nota de Crédito (códigos numéricos WSFE)
- [ ] ¿El SmtpPassword se guarda en texto plano o necesita cifrado a nivel app?
- [ ] ¿Una Agencia/Empresa puede estar activa en un grupo pero con importe distinto al del grupo? (personalización por miembro)
