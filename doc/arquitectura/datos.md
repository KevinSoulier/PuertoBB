# Modelo de datos

Todas las entidades viven en `PuertoBB.Core/Entities/`. Cada aplicación tiene su propio namespace y DbContext. Los enums compartidos viven en `PuertoBB.Core/Enums/`.

---

## Cámara Portuaria — `Core/Entities/CamaraPortuaria/`

### `Empresa`
```csharp
public class Empresa : BaseEntity
{
    public string  Nombre      { get; set; } = string.Empty;
    public string  RazonSocial { get; set; } = string.Empty;
    public string  Cuit        { get; set; } = string.Empty;
    public string? Domicilio   { get; set; }
    public bool    Activa      { get; set; } = true;

    public ICollection<EmailEmpresa>  Emails  { get; set; } = [];
    public ICollection<EmpresaGrupo>  Grupos  { get; set; } = [];
    public ICollection<Recibo>        Recibos { get; set; } = [];
}
```

### `EmailEmpresa`
```csharp
public class EmailEmpresa : BaseEntity
{
    public int     EmpresaId { get; set; }
    public Empresa Empresa   { get; set; } = null!;
    public string  Email     { get; set; } = string.Empty;
    public bool    Activo    { get; set; } = true;
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

    public ICollection<EmpresaGrupo> Empresas { get; set; } = [];
    public ICollection<Recibo>       Recibos  { get; set; } = [];
}
```

### `EmpresaGrupo` *(join N:M)*
```csharp
public class EmpresaGrupo : BaseEntity
{
    public int              EmpresaId          { get; set; }
    public Empresa          Empresa            { get; set; } = null!;
    public int              GrupoFacturacionId { get; set; }
    public GrupoFacturacion Grupo              { get; set; } = null!;
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

    public int     PeriodoAnio { get; set; }
    public int     PeriodoMes  { get; set; }
    public decimal Importe     { get; set; }
    public string  Detalle     { get; set; } = string.Empty;

    // Comprobante AFIP
    public int             PuntoDeVenta        { get; set; }
    public TipoComprobante TipoComprobante     { get; set; }
    public int             CodigoAfip          { get; set; } // código numérico AFIP (ej. 11)
    public long            NumeroComprobante   { get; set; }
    public string          CAE                 { get; set; } = string.Empty;
    public DateTime        FechaVencimientoCAE { get; set; }
    public DateTime        FechaEmision        { get; set; }

    public ReciboEstado Estado { get; set; } = ReciboEstado.Emitido;

    // Control de pagos
    public DateTime  FechaVencimientoPago { get; set; }  // = FechaEmision + Configuracion.DiasVencimiento
    public DateTime? FechaPago            { get; set; }  // null hasta que Laura marque como pagado

    public NotaDeCredito? NotaDeCredito { get; set; }
}
// Índice único: (EmpresaId, GrupoFacturacionId, PeriodoAnio, PeriodoMes)
```

### `NotaDeCredito`
```csharp
public class NotaDeCredito : BaseEntity
{
    public int    ReciboOriginalId { get; set; }
    public Recibo ReciboOriginal   { get; set; } = null!;

    public int             PuntoDeVenta        { get; set; }
    public TipoComprobante TipoComprobante     { get; set; }
    public int             CodigoAfip          { get; set; }
    public long            NumeroComprobante   { get; set; }
    public string          CAE                 { get; set; } = string.Empty;
    public DateTime        FechaVencimientoCAE { get; set; }
    public DateTime        FechaEmision        { get; set; }
}
```

### `Configuracion` *(singleton, Id = 1)*
```csharp
public class Configuracion : BaseEntity
{
    public string RazonSocial  { get; set; } = string.Empty;
    public string Cuit         { get; set; } = string.Empty;
    public int    PuntoDeVenta { get; set; }

    // Tipos AFIP (configurables; default = clase C / Exento IVA).
    // En la UI se elige el comprobante por su clase (Recibo/Factura A·B·C); la Nota de
    // Crédito se deriva de la clase (ver Core/Afip/CatalogoComprobantesAfip).
    public int CodigoAfipRecibo          { get; set; } = 15; // Recibo C
    public int CodigoAfipNotaDeCredito   { get; set; } = 13; // Nota de Crédito C

    // Certificado AFIP/WSAA
    public string? AfipCertificadoRuta     { get; set; } // ruta al archivo .p12
    public string? AfipCertificadoPassword { get; set; } // contraseña del .p12
    public bool    AfipUsarHomologacion    { get; set; } = false; // solo para desarrollo/testing

    // Control de pagos
    public int DiasVencimiento { get; set; } = 30;

    // Mail saliente
    public string? SmtpHost        { get; set; }
    public int     SmtpPort        { get; set; }
    public string? SmtpUsuario     { get; set; }
    public string? SmtpPassword    { get; set; } // texto plano; aceptable para app unipersonal
    public string? EmailRemitente  { get; set; }
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
    public string? Domicilio   { get; set; }
    public bool    Activa      { get; set; } = true;

    public ICollection<EmailAgencia> Emails   { get; set; } = [];
    public ICollection<AgenciaGrupo> Grupos   { get; set; } = [];
    public ICollection<Voucher>      Vouchers { get; set; } = [];
    public ICollection<Recibo>       Recibos  { get; set; } = [];
}
```

### `EmailAgencia`
```csharp
public class EmailAgencia : BaseEntity
{
    public int     AgenciaId { get; set; }
    public Agencia Agencia   { get; set; } = null!;
    public string  Email     { get; set; } = string.Empty;
    public bool    Activo    { get; set; } = true;
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
// Índice único: (Nombre)
```

### `ContadorVoucher` *(singleton, Id = 1)*
```csharp
public class ContadorVoucher : BaseEntity
{
    public int UltimoNumero { get; set; }
    // Editable: permite fijar el valor inicial al migrar del sistema manual
    // Secuencia global única — no hay series con letra prefija
}
```

### `Voucher`
```csharp
public class Voucher : BaseEntity
{
    public int     AgenciaId { get; set; }
    public Agencia Agencia   { get; set; } = null!;

    public int   BarcoId { get; set; }
    public Barco Barco   { get; set; } = null!;

    public int      Numero  { get; set; }  // auto-generado desde ContadorVoucher; editable para importar histórico
    public decimal  Importe { get; set; }  // monto recibido
    public DateTime Fecha   { get; set; }  // fecha de entrada del barco

    public int PeriodoAnio { get; set; }   // derivado de Fecha al guardar
    public int PeriodoMes  { get; set; }

    public int?    ReciboId { get; set; }  // null = pendiente de consolidar
    public Recibo? Recibo   { get; set; }
}
// Índice único: (Numero)
```

### `Recibo`
```csharp
public class Recibo : BaseEntity
{
    public int               AgenciaId          { get; set; }
    public Agencia           Agencia            { get; set; } = null!;
    public int?              GrupoFacturacionId { get; set; }
    public GrupoFacturacion? Grupo              { get; set; }

    public int     PeriodoAnio { get; set; }
    public int     PeriodoMes  { get; set; }
    public decimal Importe     { get; set; }  // = SUM(Vouchers.Importe) cuando EsConsolidadoVouchers=true
    public string  Detalle     { get; set; } = string.Empty;
    // Cuando EsConsolidadoVouchers=true:
    // Detalle = "Vouchers Nros: 1234, 1235, 1236" (auto-generado, no lo tipea Laura)

    public bool EsConsolidadoVouchers { get; set; }

    // Apoderado fiscal (copiado desde Configuracion al emitir, para inmutabilidad)
    public bool    EsApoderado     { get; set; }
    public string? NombreApoderado { get; set; }
    public string? CuitApoderado   { get; set; }

    // Comprobante AFIP
    public int             PuntoDeVenta        { get; set; }
    public TipoComprobante TipoComprobante     { get; set; }
    public int             CodigoAfip          { get; set; }
    public long            NumeroComprobante   { get; set; }
    public string          CAE                 { get; set; } = string.Empty;
    public DateTime        FechaVencimientoCAE { get; set; }
    public DateTime        FechaEmision        { get; set; }

    public ReciboEstado Estado { get; set; } = ReciboEstado.Emitido;

    // Control de pagos
    public DateTime  FechaVencimientoPago { get; set; }
    public DateTime? FechaPago            { get; set; }

    public ICollection<Voucher> Vouchers      { get; set; } = [];
    public NotaDeCredito?       NotaDeCredito { get; set; }
}
// Índice único: (AgenciaId, GrupoFacturacionId, PeriodoAnio, PeriodoMes)
// Recibos consolidados únicos por: (AgenciaId, PeriodoAnio, PeriodoMes) WHERE EsConsolidadoVouchers=true
```

### `NotaDeCredito`
```csharp
public class NotaDeCredito : BaseEntity
{
    public int    ReciboOriginalId { get; set; }
    public Recibo ReciboOriginal   { get; set; } = null!;

    public int             PuntoDeVenta        { get; set; }
    public TipoComprobante TipoComprobante     { get; set; }
    public int             CodigoAfip          { get; set; }
    public long            NumeroComprobante   { get; set; }
    public string          CAE                 { get; set; } = string.Empty;
    public DateTime        FechaVencimientoCAE { get; set; }
    public DateTime        FechaEmision        { get; set; }
}
```

### `Configuracion` *(singleton, Id = 1)*
```csharp
public class Configuracion : BaseEntity
{
    public string RazonSocial  { get; set; } = string.Empty;
    public string Cuit         { get; set; } = string.Empty;
    public int    PuntoDeVenta { get; set; }

    // Tipos AFIP (configurables; default = clase C / Exento IVA). NC derivada de la clase.
    public int CodigoAfipRecibo        { get; set; } = 15;
    public int CodigoAfipNotaDeCredito { get; set; } = 13;

    // Certificado AFIP/WSAA
    public string? AfipCertificadoRuta     { get; set; }
    public string? AfipCertificadoPassword { get; set; }
    public bool    AfipUsarHomologacion    { get; set; } = false;

    // Apoderado fiscal
    public bool    UsarApoderado   { get; set; }
    public string? NombreApoderado { get; set; }
    public string? CuitApoderado   { get; set; }

    // Control de pagos
    public int DiasVencimiento { get; set; } = 30;

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
public enum ReciboEstado    { Emitido, Enviado, Pagado, Anulado }
// "Vencido" se calcula en tiempo de presentación: FechaVencimientoPago < DateTime.Today && Estado != Pagado/Anulado
// No es un estado persistido para evitar transiciones automáticas no deseadas

public enum TipoComprobante { Recibo, NotaDeCredito }
// Los códigos AFIP numéricos van en Configuracion.CodigoAfipRecibo / CodigoAfipNotaDeCredito
// La tabla oficial (FEParamGetTiposCbte) vive en Core/Afip/CatalogoComprobantesAfip:
//   Recibo  A=4  B=9  C=15   Factura A=1 B=6 C=11   Nota de Crédito A=3 B=8 C=13
```

---

## Decisiones de diseño

| Decisión | Elección | Motivo |
| --- | --- | --- |
| Entidades separadas por app | Sí | Cada app tiene DB propia; compartir entidades crearía acoplamiento |
| Período como dos ints | `PeriodoAnio + PeriodoMes` | Sin ambigüedades de timezone; indexa eficientemente |
| `Configuracion` singleton | Id = 1 por convención | App unipersonal, sin multi-tenant |
| `EmpresaGrupo`/`AgenciaGrupo` como entidad | Sí | Permite agregar campos futuros sin migración compleja |
| `NotaDeCredito` separada de `Recibo` | Sí | Comprobantes AFIP distintos; relación explícita |
| Múltiples emails por entidad | `EmailEmpresa`/`EmailAgencia` | Algunos destinatarios requieren copias a varios contactos |
| `Vencido` calculado, no persistido | Sí | Evita transiciones automáticas; se calcula en la capa de presentación |
| Vouchers sin series | Contador global único | Laura confirmó: un solo contador numérico, sin letra prefija |
| Códigos AFIP configurables | En `Configuracion` | Permite cambiar si el tipo fiscal cambia, sin redespliegue |
| Certificado AFIP | Ruta en `Configuracion` | Usuario lo carga via file picker desde la pantalla de configuración |
| SMTP password | Texto plano en SQLite | Aceptable: app unipersonal de escritorio, DB no expuesta externamente |
| NC por mail | Opcional en el dialog | Checkbox al anular: "Enviar notificación por mail" (default: true) |
| Apoderado en `Recibo` | Copiado desde `Configuracion` al emitir | Inmutabilidad del comprobante: el apoderado puede cambiar luego sin afectar recibos pasados |
| PDF de recibos | Regenerado a demanda (no persiste en DB) | Volumen bajo; template consistente garantiza mismo resultado; simplifica modelo |
| Estados del recibo | `Emitido` (CAE obtenido) → `Enviado` (mail OK) | Si mail falla, queda Emitido; botón "Reenviar" en dashboard; rollback solo si falla AFIP |
| Cobros extraordinarios CM | GrupoFacturacion + emisión individual | Mismo mecanismo que CP; no requiere entidad nueva |
| Emisión individual | Solo a entidades del sistema | No se emite a receptores libres; destinatario siempre existe en la DB |
| Entorno AFIP | `AfipUsarHomologacion` en Configuracion | Flag solo para desarrollo; la app de producción siempre usa producción (default: false) |
