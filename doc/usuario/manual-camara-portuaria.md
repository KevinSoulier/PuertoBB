# Manual operativo — Cámara Portuaria

Guía del día a día de la aplicación **Cámara Portuaria** (emisión de recibos a empresas
asociadas). La configuración inicial y el paso a producción están en
[paso-a-produccion.md](paso-a-produccion.md).

---

## Las pantallas (menú lateral)

| Página | Para qué sirve |
|---|---|
| **Inicio** | Control de pagos: recibos pendientes de cobro, filtrables, con días de atraso. |
| **Recibos** | Todos los recibos: emitir individual, reintentar, anular, reenviar, marcar pagado, ver PDF. |
| **Emisión masiva** | Emitir la cuota de un grupo completo para un período. |
| **Empresas** | ABM de empresas y sus emails (a esos emails se envían los recibos). |
| **Grupos** | ABM de grupos de facturación y sus ítems (qué se cobra y cuánto). |
| **Configuración** | Emisor, AFIP, correo, tema, backup. |

## Estados de un recibo

| Estado | Significado | Qué se puede hacer |
|---|---|---|
| **Pendiente** | Creado pero AFIP todavía no dio el CAE (falló o no se pidió) | Reintentar emisión |
| **Emitido** | CAE obtenido; mail no enviado aún | Enviar/reenviar, anular, marcar pagado |
| **Enviado** | Mail enviado a la empresa | Reenviar, anular, marcar pagado |
| **Pagado** | Cobrado (lo marcaste vos) | Ver PDF |
| **Vencido** | (Visual) Pasó la fecha de vencimiento sin pago | Igual que Emitido/Enviado |
| **Anulado** | Anulado con nota de crédito | Ver PDF |

## Emisión masiva (la operación mensual típica)

1. Ir a **Emisión masiva**, elegir **período** (mes/año) y **grupo**.
2. La tabla muestra una fila por empresa del grupo con su estado para ese período
   ("No emitido" si todavía no se emitió).
3. Botones:
   - **Emitir** — pide el CAE a AFIP para todas las no emitidas (sin mandar mails).
   - **Enviar** — manda por mail los recibos ya emitidos que falten enviar.
   - **Emitir y enviar** — las dos cosas juntas.
4. Si alguna fila falla (AFIP caído, sin emails cargados), el error queda visible en la fila
   y se puede **reintentar por fila** sin tocar a las demás. El reintento nunca duplica: si
   el recibo quedó Pendiente, lo retoma; si ya tiene CAE, solo reintenta el mail.

> El importe y el detalle salen de los **ítems del grupo** (página Grupos). Si el grupo
> cambió sus ítems, los recibos ya emitidos NO cambian (el comprobante es inmutable);
> solo los que aún estaban Pendientes toman los valores nuevos.

## Emisión individual (cobros extraordinarios)

1. En **Recibos**, botón **"Nuevo recibo"**.
2. Elegir la empresa, el período, el concepto y el importe (admite varios ítems).
3. Se puede emitir **más de un recibo individual** a la misma empresa en el mismo período
   (ej. cuota + papelería): cada uno es un comprobante independiente.
4. Si AFIP falla, el recibo queda **Pendiente** y se reintenta desde la fila (ícono de
   emisión). Un recibo Pendiente solo se retoma si volvés a pedir **exactamente el mismo
   cobro**; si emitís otro concepto/importe, se crea un recibo nuevo y el Pendiente queda
   ahí para reintentarlo cuando AFIP vuelva.

## Acciones por fila (página Recibos)

- **Emitir / reintentar** — pide el CAE de un Pendiente.
- **Anular** — emite la **nota de crédito** en AFIP y deja el recibo Anulado. Al anular
  podés elegir si se le avisa por mail a la empresa. Si AFIP rechaza la NC, no cambia nada
  (el recibo sigue como estaba).
- **Reenviar por correo** — vuelve a mandar el PDF (solo recibos con CAE).
- **Marcar como pagado** — registra la fecha de pago de hoy.
- **Abrir PDF** — genera y abre el comprobante (CAE + QR de AFIP).

## Control de pagos (Inicio)

- Filtros por período, empresa, estado, **"Solo vencidos"** e **"Incluir morosos"**.
- "Vencido" se calcula con la fecha de vencimiento (emisión + días configurados); no es un
  estado guardado.
- El flag **moroso** de una empresa se marca a mano en Empresas y es solo informativo
  (no bloquea la emisión).

## Backup

En **Configuración**: "Generar backup…" guarda una copia de la base donde elijas;
"Restaurar" la reemplaza (después hay que cerrar y reabrir la aplicación). Recomendado:
backup mensual como mínimo, guardado fuera de la PC.

## Si algo sale mal

- El motivo del último error de emisión o de mail queda visible en la fila del recibo.
- El detalle técnico completo queda en `%LocalAppData%\PuertoBB\CamaraPortuaria\Logs\`
  (un archivo por día) — útil para soporte.
- La versión de la aplicación está en el título de la ventana (ej. `· v1.0.0`).
