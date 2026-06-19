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
    public int?    CondicionIvaId { get; set; } // código AFIP (CatalogoCondicionesIvaReceptor, RG 5616)
    public bool    Activa      { get; set; } = true;

    public ICollection<EmailEmpresa>  Emails  { get; set; } = [];
    public ICollection<EmpresaGrupo>  Grupos  { get; set; } = [];
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
    public bool    Activo      { get; set; } = true;

    public ICollection<EmpresaGrupo>  Empresas { get; set; } = [];
    public ICollection<ReciboLinea>   Lineas   { get; set; } = [];
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

### `EmisionGrupo` *(vínculo Grupo ↔ Recibo, cascade al borrar el grupo)*
```csharp
public class EmisionGrupo : BaseEntity
{
    public int              GrupoFacturacionId { get; set; }
    public GrupoFacturacion Grupo              { get; set; } = null!;
    public int              ReciboId           { get; set; }
    public Recibo           Recibo             { get; set; } = null!;

    // Denormalizados desde el Recibo (anti-duplicados de emisión por grupo)
    public int EmpresaId   { get; set; }
    public int PeriodoAnio { get; set; }
    public int PeriodoMes  { get; set; }
}
// Índice único: (GrupoFacturacionId, EmpresaId, PeriodoAnio, PeriodoMes)
```

### `Recibo`
```csharp
public class Recibo : BaseEntity
{
    public int     EmpresaId { get; set; }
    public Empresa Empresa   { get; set; } = null!;

    // Null = emisión individual. La relación con el grupo vive en EmisionGrupo.
    public EmisionGrupo? EmisionGrupo { get; set; }

    // Snapshot fiscal del receptor (copiado al emitir, inmutable)
    public string  ReceptorNombre       { get; set; } = string.Empty;
    public string  ReceptorRazonSocial  { get; set; } = string.Empty;
    public string  ReceptorCuit         { get; set; } = string.Empty;
    public string? ReceptorDomicilio    { get; set; }
    public string? ReceptorCondicionIva { get; set; }   // texto derivado del catálogo (para PDF)
    public int?    ReceptorCondicionIvaId { get; set; } // código AFIP snapshot (RG 5616)

    public int     PeriodoAnio { get; set; }
    public int     PeriodoMes  { get; set; }
    public decimal Importe     { get; set; } // = SUM(Lineas.Importe)
    public string  Detalle     { get; set; } = string.Empty; // encabezado opcional

    public ICollection<ReciboLinea> Lineas { get; set; } = []; // snapshot del detalle

    // Comprobante AFIP
    public int             PuntoDeVenta        { get; set; }
    public TipoComprobante TipoComprobante     { get; set; }
    public int             CodigoAfip          { get; set; } // código numérico AFIP (ej. 15)
    public long            NumeroComprobante   { get; set; }
    public string          CAE                 { get; set; } = string.Empty;
    public DateTime        FechaVencimientoCAE { get; set; }
    public DateTime        FechaEmision        { get; set; }

    public EstadoFiscal EstadoFiscal { get; set; } = EstadoFiscal.Emitido; // único estado de flujo persistido

    // Trazabilidad de emisión
    public string?   UltimoErrorCae  { get; set; } // null = CAE OK
    public string?   UltimoErrorMail { get; set; } // null = mail OK o no enviado
    public DateTime? FechaEnvioMail  { get; set; } // null = mail no enviado

    // Control de pagos (eje de cobro derivado: Pendiente de cobro / Pagado / Incobrable)
    public DateTime  FechaVencimientoPago { get; set; } // = FechaEmision + DiasVencimiento
    public DateTime? FechaPago            { get; set; } // null hasta marcar pagado
    public DateTime? FechaIncobrable      { get; set; } // null = no dado de baja; excluyente con FechaPago
    public string?   MotivoIncobrable     { get; set; } // motivo opcional de la baja

    public NotaDeCredito? NotaDeCredito { get; set; }
}
// Índice único: (PuntoDeVenta, NumeroComprobante, CodigoAfip) WHERE NumeroComprobante > 0
```

### `ReciboLinea`
```csharp
public class ReciboLinea : BaseEntity
{
    public int     ReciboId       { get; set; }
    public Recibo  Recibo         { get; set; } = null!;
    public string  Descripcion    { get; set; } = string.Empty;
    public decimal Cantidad       { get; set; } = 1;
    public decimal PrecioUnitario { get; set; }
    public decimal Importe        { get; set; } // = Cantidad × PrecioUnitario (snapshot)
    public int     Orden          { get; set; }
}
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

### `PuntoDeVenta`
```csharp
public class PuntoDeVenta : BaseEntity
{
    public int     ConfiguracionId    { get; set; }
    public string  Nombre             { get; set; } = string.Empty;
    public int     Numero             { get; set; }
    public bool    UsarHomologacion   { get; set; }
    public string? CertificadoRuta    { get; set; } // solo nombre de archivo (display)
    public byte[]? CertificadoContenido { get; set; } // contenido del .p12 o .crt/.pem (texto plano)
    public string? CertificadoPassword { get; set; } // texto plano
    public string? CertificadoKeyRuta  { get; set; } // solo nombre de archivo (display), modo CRT+KEY
    public byte[]? CertificadoKeyContenido { get; set; } // contenido del .key (texto plano), modo CRT+KEY
    public bool    Activo             { get; set; }
}
```

### `Configuracion` *(singleton, Id = 1)*
```csharp
public class Configuracion : BaseEntity
{
    public string    RazonSocial       { get; set; } = string.Empty;
    public string    Cuit              { get; set; } = string.Empty;
    public string?   IngresosBrutos    { get; set; }
    public DateTime? InicioActividades { get; set; }

    public int CodigoAfipRecibo        { get; set; } = 15; // Recibo C
    public int CodigoAfipNotaDeCredito { get; set; } = 13; // Nota de Crédito C

    public List<PuntoDeVenta> PuntosDeVenta { get; set; } = new();
    // [NotMapped] PuntoDeVentaActivo => PuntosDeVenta.FirstOrDefault(p => p.Activo)

    public int DiasVencimiento { get; set; } = 15;

    // Cuentas de correo saliente (cada una con su SMTP/auth; una activa). Ver CuentaCorreo (D-27).
    public List<CuentaCorreo> CuentasCorreo { get; set; } = new();
    // [NotMapped] CuentaCorreoActiva => CuentasCorreo.FirstOrDefault(c => c.Activo)
}
```

`CuentaCorreo` (una por app; espejo de `PuntoDeVenta`, secretos en texto plano):

```csharp
public class CuentaCorreo : BaseEntity
{
    public int    ConfiguracionId { get; set; }
    public string Nombre { get; set; } = "";   // etiqueta: "Ventas", "Administración"
    public bool   Activo { get; set; }          // solo una activa por app

    // Transporte SMTP
    public string? SmtpHost; public int SmtpPort; public int SmtpSeguridad; public string? EmailRemitente;

    // Autenticación: 0=Ninguna, 1=Básica, 2=OAuth2
    public int Autenticacion; public string? SmtpUsuario; public string? SmtpPassword;

    // OAuth2: proveedor 0=Microsoft,1=Google,2=Personalizado,3=OutlookPersonal; flujo 0=Interactivo,1=Cliente
    public int OAuthProveedor; public int OAuthFlujo;
    public string? OAuthClientId, OAuthClientSecret, OAuthTenantId, OAuthScope;
    public string? OAuthAuthorizeEndpoint, OAuthTokenEndpoint; // solo Personalizado
    public string? OAuthRefreshToken;  // del flujo interactivo
    public string? OAuthUsuario;       // email autenticado (login XOAUTH2)
}
```

---

## Centro Marítimo — `Core/Entities/CentroMaritimo/`

### `Agencia`
```csharp
public class Agencia : BaseEntity
{
    public string  Nombre       { get; set; } = string.Empty;
    public string  RazonSocial  { get; set; } = string.Empty;
    public string  Cuit         { get; set; } = string.Empty;
    public string? Domicilio    { get; set; }
    public int?    CondicionIvaId { get; set; } // código AFIP (CatalogoCondicionesIvaReceptor, RG 5616)
    public bool    Activa       { get; set; } = true;

    public ICollection<EmailAgencia> Emails   { get; set; } = [];
    public ICollection<AgenciaGrupo> Grupos   { get; set; } = [];
    public ICollection<Voucher>      Vouchers { get; set; } = [];
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
    public bool    Activo      { get; set; } = true;

    public ICollection<AgenciaGrupo> Agencias { get; set; } = [];
    public ICollection<ReciboLinea>  Lineas   { get; set; } = [];
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

### `EmisionGrupo` *(vínculo Grupo ↔ Recibo, cascade al borrar el grupo)*
```csharp
public class EmisionGrupo : BaseEntity
{
    public int              GrupoFacturacionId { get; set; }
    public GrupoFacturacion Grupo              { get; set; } = null!;
    public int              ReciboId           { get; set; }
    public Recibo           Recibo             { get; set; } = null!;

    public int AgenciaId   { get; set; }
    public int PeriodoAnio { get; set; }
    public int PeriodoMes  { get; set; }
}
// Índice único: (GrupoFacturacionId, AgenciaId, PeriodoAnio, PeriodoMes)
```

### `Barco`
```csharp
public class Barco : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
}
// Índice único: (Nombre)
```

### `ContadorVoucher` *(singleton, Id = 1)*
```csharp
public class ContadorVoucher : BaseEntity
{
    public int UltimoNumero { get; set; }
}
```

### `Voucher`
```csharp
public class Voucher : BaseEntity
{
    public int     AgenciaId { get; set; }
    public Agencia Agencia   { get; set; } = null!;
    public int     BarcoId   { get; set; }
    public Barco   Barco     { get; set; } = null!;

    public int      Numero  { get; set; }  // auto-generado desde ContadorVoucher
    public decimal  Importe { get; set; }
    public DateTime Fecha   { get; set; }

    public int PeriodoAnio { get; set; }
    public int PeriodoMes  { get; set; }

    public int?    ReciboId { get; set; } // null = pendiente de consolidar
    public Recibo? Recibo   { get; set; }
}
// Índice único: (Numero)
```

### `Recibo`
```csharp
public class Recibo : BaseEntity
{
    public int     AgenciaId { get; set; }
    public Agencia Agencia   { get; set; } = null!;

    // Null = individual o consolidado de vouchers. La relación con el grupo vive en EmisionGrupo.
    public EmisionGrupo? EmisionGrupo { get; set; }

    // Snapshot fiscal del receptor (copiado al emitir, inmutable)
    public string  ReceptorNombre       { get; set; } = string.Empty;
    public string  ReceptorRazonSocial  { get; set; } = string.Empty;
    public string  ReceptorCuit         { get; set; } = string.Empty;
    public string? ReceptorDomicilio    { get; set; }
    public string? ReceptorCondicionIva { get; set; }   // texto derivado del catálogo (para PDF)
    public int?    ReceptorCondicionIvaId { get; set; } // código AFIP snapshot (RG 5616)

    public int     PeriodoAnio { get; set; }
    public int     PeriodoMes  { get; set; }
    public decimal Importe     { get; set; } // = SUM(Lineas.Importe)
    public string  Detalle     { get; set; } = string.Empty;

    public ICollection<ReciboLinea> Lineas { get; set; } = [];

    public bool EsConsolidadoVouchers { get; set; }

    // Comprobante AFIP
    public int             PuntoDeVenta        { get; set; }
    public TipoComprobante TipoComprobante     { get; set; }
    public int             CodigoAfip          { get; set; }
    public long            NumeroComprobante   { get; set; }
    public string          CAE                 { get; set; } = string.Empty;
    public DateTime        FechaVencimientoCAE { get; set; }
    public DateTime        FechaEmision        { get; set; }

    public EstadoFiscal EstadoFiscal { get; set; } = EstadoFiscal.Emitido; // único estado de flujo persistido

    public string?   UltimoErrorCae  { get; set; }
    public string?   UltimoErrorMail { get; set; }
    public DateTime? FechaEnvioMail  { get; set; }

    public DateTime  FechaVencimientoPago { get; set; }
    public DateTime? FechaPago            { get; set; }
    public DateTime? FechaIncobrable      { get; set; } // eje de cobro; excluyente con FechaPago
    public string?   MotivoIncobrable     { get; set; }

    public ICollection<Voucher> Vouchers      { get; set; } = [];
    public NotaDeCredito?       NotaDeCredito { get; set; }
}
// Índice único: (PuntoDeVenta, NumeroComprobante, CodigoAfip) WHERE NumeroComprobante > 0
// Índice único parcial: (AgenciaId, PeriodoAnio, PeriodoMes) WHERE EsConsolidadoVouchers=1 AND EstadoFiscal<>'Anulado'
```

### `ReciboLinea`
```csharp
public class ReciboLinea : BaseEntity
{
    public int     ReciboId       { get; set; }
    public Recibo  Recibo         { get; set; } = null!;
    public string  Descripcion    { get; set; } = string.Empty;
    public decimal Cantidad       { get; set; } = 1;
    public decimal PrecioUnitario { get; set; }
    public decimal Importe        { get; set; }
    public int     Orden          { get; set; }
}
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

### `PuntoDeVenta`
```csharp
public class PuntoDeVenta : BaseEntity
{
    public int     ConfiguracionId     { get; set; }
    public string  Nombre              { get; set; } = string.Empty;
    public int     Numero              { get; set; }
    public bool    UsarHomologacion    { get; set; }
    public string? CertificadoRuta     { get; set; } // solo nombre de archivo (display)
    public byte[]? CertificadoContenido { get; set; } // contenido (texto plano)
    public string? CertificadoPassword { get; set; } // texto plano
    public string? CertificadoKeyRuta  { get; set; } // solo nombre de archivo (display)
    public byte[]? CertificadoKeyContenido { get; set; } // contenido del .key (texto plano)
    public bool    Activo              { get; set; }
}
```

### `Configuracion` *(singleton, Id = 1)*
```csharp
public class Configuracion : BaseEntity
{
    public string    RazonSocial       { get; set; } = string.Empty;
    public string    Cuit              { get; set; } = string.Empty;
    public string?   IngresosBrutos    { get; set; }
    public DateTime? InicioActividades { get; set; }

    public int CodigoAfipRecibo        { get; set; } = 15;
    public int CodigoAfipNotaDeCredito { get; set; } = 13;

    public List<PuntoDeVenta> PuntosDeVenta { get; set; } = new();
    // [NotMapped] PuntoDeVentaActivo => PuntosDeVenta.FirstOrDefault(p => p.Activo)

    // Vouchers
    public decimal ImporteVoucherPredeterminado { get; set; } = 0;

    public int DiasVencimiento { get; set; } = 15;

    // Cuentas de correo saliente (igual que CentroMaritimo: ver CuentaCorreo, D-27). Una activa.
    public List<CuentaCorreo> CuentasCorreo { get; set; } = new();
    // [NotMapped] CuentaCorreoActiva => CuentasCorreo.FirstOrDefault(c => c.Activo)
}
```

---

## Enums compartidos — `Core/Enums/`

```csharp
// Único estado de flujo persistido del recibo (eje fiscal/AFIP).
public enum EstadoFiscal
{
    Pendiente, // creado sin CAE todavía (reintentable)
    Emitido,   // CAE obtenido
    Anulado    // anulado con Nota de Crédito
}
// Los otros ejes NO se persisten como estado; se derivan en EstadoReciboHelper:
//   Envío  = FechaEnvioMail!=null ? Enviado : UltimoErrorMail!=null ? Fallido : NoEnviado
//   Cobro  = FechaIncobrable!=null ? Incobrable : FechaPago!=null ? Pagado : PendienteDeCobro
//   Vencido = EstadoFiscal==Emitido && Cobro==PendienteDeCobro && FechaVencimientoPago < hoy
public enum EstadoEnvio { NoEnviado, Enviado, Fallido }
public enum EstadoCobro { PendienteDeCobro, Pagado, Incobrable }

public enum TipoComprobante { Recibo, NotaDeCredito }
// Códigos AFIP: Recibo A=4 B=9 C=15 | Factura A=1 B=6 C=11 | NC A=3 B=8 C=13
// Tabla completa: Core/Afip/CatalogoComprobantesAfip.cs
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
| `EmisionGrupo` como join desacoplado | Sí | Recibo es entidad de auditoría autocontenida; relación con el grupo vive en `EmisionGrupo` (cascade al borrar grupo, recibos sobreviven) |
| Snapshot `Receptor*` en `Recibo` | Sí | Datos fiscales del receptor copiados al emitir; PDF/AFIP nunca leen de la navegación |
| `ReciboLinea` como entidad propia | Sí | Snapshot inmutable del detalle; el detalle mostrado/enviado sale siempre de las líneas |
| Estado `Pendiente` | Persiste antes de pedir CAE | Hace la emisión idempotente y reintentable tras un fallo |
| `PuntoDeVenta` como entidad | Sí | Permite cargar varios (ej. homologación + producción); el activo determina número, ambiente y certificado |
| Secretos (SMTP password, OAuth client secret / refresh token, certificado, contraseña del cert) | Texto plano en la base | Sin cifrado por decisión del usuario (D-24); el certificado se guarda como BLOB en `PuntoDeVenta` e ingresa en el backup |
| Autenticación de correo | `Ninguna`/`Básica`/`OAuth2` en `Configuracion` | OAuth2 (XOAUTH2) es obligatorio para Microsoft 365/Outlook; básica cubre Gmail (app password), servicios y SMTP propios (D-26) |
| NC por mail | Opcional en el dialog | Checkbox al anular: "Enviar notificación por mail" (default: true) |
| PDF de recibos | Regenerado a demanda | Volumen bajo; template consistente garantiza mismo resultado |
