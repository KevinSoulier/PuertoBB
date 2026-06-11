# Diseño — Recibo como agregado autocontenido (ítems + snapshot) y NC

> Resuelve HK-1 (detalle inconsistente según pantalla), HK-2 (recibo de un solo ítem) y HK-4 (NC sin snapshot).
> **Estado: PROPUESTA — requiere tu OK antes de implementar** (implica migración EF con backfill en CP y CM).
> Decisión de negocio fijada: **NC = solo anulación total** (sin parcial).

## Problema (con evidencia)

- `Recibo` tiene un único `decimal Importe` + `string Detalle`; no hay entidad de ítems. (`Core/Entities/*/Recibo.cs`)
- El detalle se **deriva en runtime** distinto según el flujo:
  - `CentroMaritimoReciboService.cs:129` persiste `Detalle = "Vouchers Nros: 1, 2, 3"`.
  - `CentroMaritimoPdfService.cs:159` **ignora** `recibo.Detalle` cuando hay vouchers y arma ítems desde la colección `vouchers`.
  - El PDF simple (`GenerarPdfReciboAsync`) sí usa `recibo.Detalle`. → el mismo recibo se ve distinto según cómo se abra (HK-1).
- `CamaraPortuariaPdfService.GenerarPdfReciboAsync` no soporta ítems (solo `ConceptoGeneral`). (HK-2 en CP)
- `NotaDeCredito` no guarda detalle/importe/ítems; el PDF de NC deriva del recibo original. (HK-4)

## Diseño objetivo

El `Recibo` (y la `NotaDeCredito`) pasan a ser **comprobantes autocontenidos**: al emitirse, **persisten su propio
detalle completo** y se renderizan **siempre igual** desde cualquier pantalla, PDF y mail.

### Nueva entidad: `ReciboItem` (una por dominio: CP y CM)

```csharp
public class ReciboItem : BaseEntity
{
    public int      ReciboId      { get; set; }
    public Recibo   Recibo        { get; set; } = null!;
    public string   Descripcion   { get; set; } = string.Empty;
    public decimal  Importe       { get; set; }   // subtotal del ítem
    public int      Orden         { get; set; }   // para mantener el orden de carga
    // (opcional, si se necesita) public decimal? Cantidad; public decimal? PrecioUnitario;
}
```

- `Recibo` agrega `public ICollection<ReciboItem> Items { get; set; } = [];`
- `Recibo.Importe` pasa a ser **derivado** = `Items.Sum(i => i.Importe)` (se persiste el total calculado al emitir, para inmutabilidad).
- `Recibo.Detalle` (string) se **mantiene** como encabezado/leyenda opcional del comprobante (no como única fuente del detalle).

### Snapshot inmutable

- Al **emitir**, los `Items` quedan congelados junto con el resto del comprobante. No se recalculan al abrir.
- En CM, el cierre de período **materializa** un `ReciboItem` por voucher (Descripción = `"Voucher {Nro} — {Barco} — {Fecha}"`, Importe = voucher.Importe) en vez de derivarlo en el PDF. La relación `Recibo.Vouchers` se mantiene solo para trazabilidad.

### Nota de crédito (HK-4, solo anulación total)

- `NotaDeCredito` agrega snapshot propio: `ICollection<NotaDeCreditoItem>` (o reutilizar `ReciboItem` con FK a NC), `Importe`, y `Detalle`, **copiados del recibo** al anular.
- Anulación total: la NC replica **todos** los ítems del recibo por el total; el recibo pasa a `Anulado`. Ya validado: requiere CAE (F-10).
- El PDF de NC usa su propio snapshot (no `nota.ReciboOriginal`).

## Cambios por capa

1. **Core**: `ReciboItem` (CP y CM) + `NotaDeCreditoItem` (o ítems en NC); navegación en `Recibo`/`NotaDeCredito`.
2. **Infrastructure**: `IEntityTypeConfiguration` para los ítems (FK, índice por `ReciboId`, `Descripcion` maxlen, `Importe` —TEXT en SQLite—, `Orden`). **Migración nueva CP + CM**.
3. **Backfill (en la migración o paso de datos)**: por cada recibo existente sin ítems, crear **un** `ReciboItem` con `Descripcion = Detalle` actual e `Importe = Importe`. Para consolidados CM con vouchers, opcionalmente generar un ítem por voucher asociado. Así los comprobantes históricos no quedan vacíos.
4. **Services**:
   - Emisión individual/masiva: recibir `IReadOnlyList<(string desc, decimal importe)>` (o seguir aceptando `detalle`+`importe` y crear un único ítem) → crear `Items`, total derivado.
   - Cierre de período (CM): crear un ítem por voucher.
   - Anular: copiar ítems del recibo a la NC.
5. **Afip.Documentos / PdfService**: ambos PDFs (recibo y NC) renderizan **siempre** desde `Items` (CP gana soporte de ítems). Quitar la rama `vouchers.Count == 0 ? Detalle : null`.
6. **AFIP (WSFE)**: el `ImporteTotal` sigue siendo la suma; AFIP para comprobante C no lleva detalle de ítems (va el total), así que el mapeo AFIP **no cambia** salvo usar el total derivado. (Los ítems son para el PDF/almacenamiento, no para WSFE.)
7. **UI**: en la página de emisión individual de recibo, permitir cargar **N ítems** (grid editable: descripción + importe, con total calculado). En ambas apps. Mantener un modo rápido de "un solo ítem" para no complicar el caso simple.
8. **Tests (L-G)**: emisión con N ítems, total = suma, snapshot estable (abrir desde distintos flujos da el mismo detalle), anulación total copia ítems, backfill de históricos.

## Orden de implementación sugerido (cada paso build+tests verdes)

1. Core: entidades de ítems (sin tocar lógica todavía) — no compila en uso aún, solo tipos.
2. Infra: configs EF + `dotnet ef migrations add RecibosItems` en CP y CM + backfill.
3. Services: emisión y cierre crean ítems; total derivado. Anular copia ítems.
4. PDF: render desde ítems (CP y CM). Quitar derivación de vouchers.
5. UI: edición de N ítems.
6. Tests.

## Riesgos / decisiones para el usuario

- **Migración con backfill** sobre datos reales: revisar antes de correr en la base de producción (hacer backup primero — ya hay botón de backup).
- ¿Querés `Cantidad`/`PrecioUnitario` por ítem, o alcanza con descripción + importe? (Propuesta: empezar con descripción + importe.)
- ¿La emisión masiva por grupo sigue siendo monto único (un ítem) o también multi-ítem? (Propuesta: grupo = un ítem con el nombre del grupo; multi-ítem solo en emisión individual.)
