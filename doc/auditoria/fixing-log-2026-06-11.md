# Fixing Log — Auditoría 2026-06-11

> Ejecutado sobre el **working tree** (sin commits: el árbol ya tenía los cambios de D-21 sin
> commitear y mezclar commits de fixing con D-21 hubiera ensuciado el historial — commitear
> todo junto cuando el usuario lo decida). Build con `-c Release` (Visual Studio mantenía
> lockeadas las DLLs Debug de CentroMaritimo.UI).
>
> Suite final: **120/120 verdes** (100 PuertoBB.Tests + 20 Afip.Documentos.Tests).
> Línea base previa: 98 (81+17).

| Ítem | Estado | Detalle |
|---|---|---|
| **N-1** (P1) | ✅ hecho | Test rojo primero (CP+CM: `EmitirIndividual_PendientePrevioConContenidoDistinto_CreaReciboNuevo[_CM]`, confirmó el bug: 1 recibo en vez de 2). Fix: guard `grupoId is null && !MismoContenido(...)` → crea recibo nuevo en vez de pisar el Pendiente; helper `MismoContenido` espejado en ambos servicios. El resume de mismo contenido (`ConPendientePrevio_RetomaSinDuplicar`) sigue verde. |
| **N-3** (P2) | ✅ hecho | Test rojo primero con **DbContexts separados** (`CerrarPeriodo_ReintentoConVoucherNuevo_ConContextosSeparados_IncluyeTodosLosVouchers`, confirmó count=1 y PDF con 1 voucher). Fix: tras `MarcarConsolidadosAsync`, recargar el consolidado vía `GetConsolidadoAsync` (mismo contexto de `_recibos` → fix-up completa la colección) y armar líneas desde `existente.Vouchers` (sin `Concat`). Cierra también la brecha H-01. |
| **N-4** (P2) | ✅ hecho | `FileLogger.Log` (ambas apps): escribe **Warning+** siempre (con o sin excepción); Information/Debug solo con excepción. Los rechazos AFIP (`LogError` sin excepción) ahora quedan en el log. Verificación en runtime pendiente (smoke). |
| **N-2** (P2) | ✅ mitigado | `LogWarning` con PV/Nro/CAE de la NC **antes** de `AnularConNotaAsync` (ambas apps): si la persistencia local falla, el comprobante autorizado queda registrado en el log. El patrón Pendiente-first completo para NC sigue **diferido** (requiere esquema). |
| **P2-2** | ✅ hecho | `IWsfeClient.ConsultarComprobanteAsync` + record `WsfeComprobanteConsultado`; `WsfeSoapClient` implementa con `FECompConsultarAsync` (602 → null); `WsfeMapper.ToComprobanteConsultado`; `MockWsfeClient` actualizado; `WsfeService.SolicitarCaeAsync` reconcilia in-flight en el catch (match por importe+doc+fecha; la consulta nunca enmascara el error original) + passthrough en `IWsfeService`. 4 tests nuevos (`WsfeReconciliacionTests`). Reconciliación "en frío" documentada como limitación en `paso-a-produccion.md`. |
| **N-5** (P3) | ✅ hecho | `ComprobanteTemplate.cs`: guard `FechaVencimientoCae > DateTime.MinValue` — ya no imprime "01/01/0001". Smoke test `GenerarPdf_SinVencimientoCae_*`. |
| **P3-7** | ✅ hecho | `WsfeMapper.ToFECAERequest`: `Math.Round(…, 2, AwayFromZero)` antes del cast a double (ImpTotal/ImpOpEx). |
| **N-6** (P3) | ✅ hecho | Invariante "total = suma de líneas" anclado con asserts en 4 tests existentes (2 por app). Template sin cambios (resto de P3-19 sigue aceptado). |
| **N-7** (P3) | ✅ hecho | Ambos PdfService: `nc.ReciboOriginal ?? throw InvalidOperationException(...)` en el PDF de NC. |
| **N-9** (P3) | ✅ hecho | **D-22** en registro-decisiones (columna Estado de vouchers eliminada). |
| **P2-10** | ✅ cerrado | **D-23** en registro-decisiones (datos reales del seed se mantienen; repo privado, seed solo en demo). |
| **N-12** (P3) | ✅ hecho | `VouchersPage.xaml`: filas renumeradas (sin hueco). |
| **P3-15 (parcial)** | ✅ hecho | `DialogService` CP: `LimpiarPreviewsViejos()` borra subdirs `pbb_preview_*` con más de 1 día antes de cada preview. El visor embebido sigue diferido. |
| **N-10** (tests) | ✅ hecho | CP: `ReenviarMail_ReciboEmitido_*`, `MarcarPagado_ReciboEnviado_*`, `MarcarPagado_ReciboPendiente_Falla`, `Anular_FalloAfipEnNc_*`. CM: `ReenviarMail_Consolidado_UsaPdfDescargaConTodosLosVouchers`, `MarcarPagado_ReciboPendiente_Falla_CM`, `EmitirDeGrupo_AgenciaDelGrupo_*`, `Anular_FalloAfipEnNc_*_CM`, `AnularConsolidado_ConContextosSeparados_DesvinculaVouchers`, `EmitirIndividual_ConContextosSeparados_CM`. Repo: `Recibo_IndiceNumeracionAfip_BloqueaNumeroDuplicado` (P2-5; Nro=0 repetible, trío único bloqueado). |
| **N-11** (tests) | ✅ hecho | QR: theory de importes extremos (10M y 1234.56 — sin exponente, decimal exacto). PDF multipágina: `PdfMultipaginaTests` (60 ítems → ≥2 páginas, validado con PdfSharp). |
| **Checklist 10 (versión)** | ✅ hecho | `<Version>1.0.0</Version>` en ambos csproj + ` · v1.0.0` en el título (antes del sufijo MODO DEMO). |
| **Checklist 2 (paso a producción)** | ✅ hecho | `doc/usuario/paso-a-produccion.md`. |
| **Checklist 9 (manuales)** | ✅ hecho | `doc/usuario/manual-camara-portuaria.md` + `manual-centro-maritimo.md` (labels verificados contra las Views). |
| **Checklist 1 (distribución)** | ⏭️ excluido | Decisión del usuario: fuera de este plan. |
| **N-8** (docs) | ✅ hecho | `cierre-periodo.md` (iteraciones ✅, anulación real, tabla de acciones), `convenciones.md` (FileLoggerProvider en lugar de Serilog + política Warning+), `estado-implementacion.md` (ídem), `funcionalidad-compartida.md` (definición precisa de Vencido), `prompt_inicializacion.md` y `doc/mejoras/informe-auditoria.md` marcados como históricos. |

## Pendientes que quedan (sin cambios respecto del plan)

- **N-2 completo** — Pendiente-first para NC (esquema en `NotaDeCredito`).
- **P3-15** — visor PDF embebido CM→CP (solo entró la limpieza de temporales).
- **Distribución/instalador** — excluido por decisión del usuario.
- **Pruebas externas** — AFIP homologación con `.p12` real y SMTP real (las hace el usuario:
  `afip-configuracion.md` + `paso-a-produccion.md`).
- **Smoke manual** (Etapa 9.3 del plan) — pendiente de correr por el usuario con la app
  cerrada en VS: dos individuales distintos mismo período (N-1), cierre CM + preview,
  `Afip="Real"` sin certificado → error legible + línea en el log (N-4), título con versión,
  backup/restore.
