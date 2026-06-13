# Plan de Auditoría Ponderada — PuertoBB (2026-06-11)

> **Cómo ejecutarlo:** en una sesión nueva de Claude Code, decir:
> *"Ejecutá el plan de auditoría de `doc/auditoria/plan-auditoria-2026-06-11.md` de punta a punta y generá el informe."*

**Objetivo:** producir `doc/auditoria/AUDITORIA-2026-06-11.md` con hallazgos priorizados P0–P3
(cada uno con archivo:línea, descripción, impacto y fix propuesto) **más una sección nueva
"Checklist de entrega"** con todo lo que falta para que la app sea entregable a producción.
**No se modifica código ni se toca git** — el informe es el insumo del plan de fixing posterior.

**Tipo de auditoría: completa ponderada.** La auditoría extensiva del 2026-06-10
(`AUDITORIA-2026-06-10.md`) ya cubrió todo el código y sus hallazgos fueron corregidos
(`fixing-log.md`: P0 1/1, P1 9/9, P2 12/14, P3 8/22 — suite 93/93 verde). Esta pasada **no
repite ciegamente** ese trabajo: profundiza en lo que cambió desde entonces, verifica que los
fixes no regresionaron, evalúa los diferidos, y suma el ángulo de entregabilidad que la
auditoría anterior no cubrió. Las áreas listadas como **"Revisado y OK"** en el informe
anterior se re-miran **solo si el diff actual las tocó**.

## Contexto que el ejecutor debe cargar primero

1. `doc/auditoria/AUDITORIA-2026-06-10.md` — informe anterior completo (hallazgos, falsos
   positivos descartados, lista "Revisado y OK").
2. `doc/auditoria/fixing-log.md` — qué fix se aplicó para cada ítem y dónde.
3. `doc/decisiones/registro-decisiones.md` — en particular **D-20** (clave de emisión
   individual "solo Pendiente") y **D-21** (baja del apoderado fiscal + reorganización de
   Configuración + migración CM regenerada).
4. `git status` + `git diff HEAD` — el working tree tiene cambios **sin commitear ni auditar**
   (el último commit `493b131` es anterior a D-21).

**Cambios sin auditar conocidos al armar este plan:**

- **D-21 (2026-06-11):** eliminación completa del "apoderado fiscal" en CM (entidad
  `Configuracion` y snapshot en `Recibo`, configuración EF, `AfipConfigProvider`,
  `CentroMaritimoReciboService`, `CentroMaritimoPdfService`, pestaña de Configuración) +
  fusión de "Datos del emisor" dentro de la pestaña "AFIP / ARCA" en **ambas** apps +
  **migración `Inicial` de CM regenerada** (`20260611210050_Inicial`, borra la del 06-10).
- **Cambios que D-21 NO explica** (a investigar en fase 2): al armar este plan se observaron
  diffs en `VoucherItem`/`VouchersPage` y, fugazmente, en `SeedData`, `VouchersViewModel`,
  `ICentroMaritimoReciboService`, `EstadoReciboToBrushConverter` y `ServiceFlowTests` — estos
  últimos **volvieron al contenido de HEAD durante la sesión de planificación** (revertidos
  fuera de la sesión). ⚠️ El working tree está en movimiento: el ejecutor debe derivar el
  diff real con `git status` + `git diff HEAD` al momento de ejecutar, y tratar todo cambio
  sin decisión registrada como hallazgo a explicar.

**Diferidos heredados de la auditoría anterior (entran en fase 10):** P2-2 (reconciliación
`FECompConsultar` tras timeout AFIP), P3-15 (portar visor PDF embebido de CM a CP), y los P3
no asignados: P3-4, P3-5, P3-6, P3-7, P3-13, P3-14, P3-17, P3-19, P3-20, P3-21, P3-22.

**Decisión ya tomada por el usuario (cerrar, no re-discutir):** **P2-10** — los datos reales
en `SeedData` (CUITs/emails de empresas y agencias) **se dejan como están**; el repo es
privado y el seed solo corre en modo demo. El informe lo registra como decisión cerrada y
recomienda asentarla en `registro-decisiones.md` durante el fixing.

## Fases

1. **Línea base** — `dotnet build PuertoBB.slnx` (capturar warnings), corrida completa de
   tests, `dotnet ef migrations has-pending-model-changes` en ambos contextos, confirmar
   **una sola migración `Inicial` por contexto** (convención: una migración por release),
   `dotnet list package --vulnerable`.

2. **Auditoría del diff sin commitear** — la fase más profunda de la pasada:
   - Recorrer `git diff HEAD` archivo por archivo; cada cambio debe tener sentido y estar
     completo (sin mitades de refactor).
   - **Baja del apoderado:** grep `Apoderado` en todo el repo (código, XAML, tests, docs) —
     no debe quedar ningún residuo fuera de `registro-decisiones.md` y los informes de
     auditoría. Verificar que `AfipConfigProvider` CM quedó coherente (siempre CUIT del
     emisor) y que `BuildEmisor` ya no necesita el `Recibo`.
   - **Migración CM regenerada — riesgo de regresión clave:** la migración se regeneró
     *después* de los fixes del 06-10; verificar leyendo `20260611210050_Inicial.cs` y el
     snapshot que conserva: (a) el índice único parcial de consolidados con filtro
     `WHERE EsConsolidadoVouchers = 1 AND "Estado" <> 'Anulado'` (fix P1-3), (b) el índice
     único `(PuntoDeVenta, NumeroComprobante, CodigoAfip)` con filtro `NumeroComprobante > 0`
     (fix P2-5), (c) cero columnas de apoderado, (d) snapshot == modelo (sin pending changes).
   - **Reorganización de Configuración (ambas apps):** los bindings/commands del Emisor no
     cambiaron según D-21 — verificar que el re-armado del XAML no rompió bindings, que
     P1-8 (IngresosBrutos/InicioActividades en UI) sigue presente y que P3-12 (desuscripción
     de `PropertyChanged` en `Unloaded`) sobrevive en ambos code-behind.
   - **Cambios no documentados:** identificar qué feature son los cambios en SeedData,
     Vouchers*, `ICentroMaritimoReciboService`, converter y tests; evaluar si están completos,
     testeados y si requieren registrar una decisión (si falta, es hallazgo).

3. **Regresión de los 22 fixes del 06-10** — contra `fixing-log.md`, ítem por ítem, confirmar
   que cada fix sigue vigente en el working tree. Atención especial a los archivos que D-21
   volvió a tocar: `ConfiguracionViewModel` CM, `ReciboConfiguration` CM,
   `CentroMaritimoReciboService`, `CentroMaritimoPdfService`, `ConfiguracionPage` (ambas).
   Confirmar también que los tests de regresión agregados entonces siguen existiendo y verdes
   (`EmitirMasivo_ConContextosSeparados_*`, `CerrarPeriodo_FallaCae_*`,
   `AnularConsolidado_PermiteReemitirElPeriodo`, etc.).

4. **Lógica de negocio y flujos completos** — re-trazar de punta a punta cada flujo sobre el
   código actual (post-fixes + post-D-21), auditando contra `doc/negocio/` leído completo:
   - Emisión individual, por grupo multi-ítem y masiva (CP y CM).
   - Anulación con NC + reenvío + registro de pago.
   - Cierre de período CM: consolidación, reintento tras fallo de CAE, anular consolidado y
     reemitir el período (verificar el flujo completo que P1-1/P1-3 arreglaron).
   - Cobros extraordinarios (D-20): el "slot" individual ahora solo matchea recibos
     `Pendiente` — analizar efectos de segundo orden (¿permite duplicados ilimitados de
     recibos ya emitidos a la misma entidad/período? ¿es lo que el negocio quiere?).
   - Para cada flujo: transiciones de estado válidas, qué pasa si falla a mitad de camino,
     idempotencia del reintento.

5. **AFIP (solo deltas)** — no re-auditar lo marcado OK (WSAA/cache/DPAPI/TRA):
   - `AfipConfigProvider` CM modificado por D-21: coherencia final.
   - Efecto río abajo del fix P2-3: con `FechaVencimientoCae ?? default`, ¿puede el PDF/QR
     imprimir `01/01/0001` como vencimiento de CAE? Trazar hasta el template y el payload QR.
   - **P2-2 (diferido):** proponer en el informe el **diseño** de la reconciliación con
     `FECompConsultar` tras timeout post-aprobación (consultar último número autorizado antes
     de pedir CAE nuevo) — diseño accionable para `Afip.Net`, sin implementar.

6. **PDF** — `CentroMaritimoPdfService` cambió de firma (`BuildEmisor` sin `Recibo`):
   re-verificar generación de recibo, consolidado (siempre vía `GenerarPdfDescargaAsync`),
   NC y QR; sin leyenda de apoderado; P1-7 (comprobante asociado en NC) y P1-8
   (IngresosBrutos/InicioActividades) siguen poblados y renderizados.

7. **UI / MVVM** — profundo solo en lo tocado: `ConfiguracionPage` + `ConfiguracionViewModel`
   (ambas apps), `VouchersPage` + `VouchersViewModel` + `VoucherItem`,
   `EstadoReciboToBrushConverter` (¿cubre todos los estados tras el cambio?). Barrido liviano
   de regresión en el resto (bindings rotos, converters, CanExecute) sin re-auditar lo OK.

8. **Modelo de datos y migraciones** — entidades CM post-apoderado vs configuración EF vs
   migración vs `doc/arquitectura/datos.md` (ya actualizado en el diff): nullability
   coherente, sin columnas huérfanas, seed de singletons intacto (`Configuracion` Id=1,
   `ContadorVoucher` Id=1), y verificación de que el doc refleja el modelo real.

9. **Cobertura de tests** — qué cubren las líneas nuevas de `ServiceFlowTests` (+35);
   si el patrón "repositorios con DbContexts separados" (la brecha que ocultó el P0-1) se
   extendió a los flujos CM o sigue solo en CP; gaps ya señalados en P3-20 (importes extremos
   en QR, footer multipágina); tests que perdieron sentido tras D-21 (¿quedó algún test del
   apoderado?).

10. **Diferidos y P3 sin asignar** — evaluar cada uno y clasificarlo en el informe como
    **"corregir para entrega"** o **"aceptado/documentado"** (con justificación):
    P2-2 (diseño en fase 5), P3-15 (visor PDF CM→CP + limpieza de temporales), P3-4 (tracking
    en `GetByIdAsync`), P3-5 (queries sin `Lineas`), P3-6 (decimal TEXT en SQLite), P3-7
    (redondeo previo al cast en `WsfeMapper`), P3-13 (notificación de edición manual), P3-14
    (mails secuenciales sin progreso), P3-17 (contador de voucher con carrera), P3-19
    (magic string "RECIBO" en template), P3-20 (tests QR), P3-21 (`ImporteNuevo` sin reset),
    P3-22 (archivos >600 líneas). **P2-10: cerrarlo** con la decisión del usuario (dejar
    datos reales; repo privado).

11. **Entregabilidad / production readiness (fase NUEVA)** — relevar y volcar en la sección
    "Checklist de entrega" del informe, con estado (✅ listo / ⚠️ falta / ❌ no existe) y
    acción concreta:
    - **Distribución:** no existe instalador ni perfil de publicación — definir el mecanismo
      (`dotnet publish` self-contained por app, ZIP o instalador; requisitos de runtime en la
      máquina destino; dónde queda `appsettings.json` editable).
    - **Procedimiento de paso a producción:** `appsettings.json` → `PuertoBB:ModoDemo=false`
      + `PuertoBB:Afip=Real` — ¿está documentado paso a paso? ¿el banner "MODO DEMO"
      desaparece correctamente y el aviso de ambiente AFIP es visible?
    - **Primera corrida sin seed:** con `ModoDemo=false` la base nace vacía — ¿la app guía la
      configuración inicial (datos del emisor, punto de venta, certificado, SMTP) o el usuario
      queda ante pantallas vacías sin orientación? ¿Hay validaciones que avisen qué falta?
    - **Pendientes externos ya conocidos:** prueba AFIP real contra homologación con `.p12` y
      credenciales (mapeo y firma ya testeados); SMTP real. Listar los pasos exactos que el
      usuario debe ejecutar.
    - **Licencia QuestPDF:** verificar que `QuestPDF.Settings.License` se setea y que el uso
      encuadra en Community.
    - **Operación:** retención/tamaño de logs Serilog; estrategia de backup documentada para
      el usuario final (¿con qué frecuencia? ¿restauración probada?); ubicación de las bases
      (`%LocalAppData%\PuertoBB\<App>`) documentada.
    - **Documentación de usuario:** `doc/usuario/` solo tiene `afip-configuracion.md` — ¿falta
      un manual operativo mínimo por app (emisión, cierre, anulación, backup)?
    - **Versión visible:** ¿la app muestra versión/About en alguna parte (para soporte)?

12. **Informe final** — consolidar en `doc/auditoria/AUDITORIA-2026-06-11.md`:
    - Tabla resumen por prioridad + una sección por hallazgo
      (`ID | Prioridad | Archivo:línea | Título`, descripción, impacto, fix propuesto).
    - Sección **"Checklist de entrega"** (salida de la fase 11).
    - Sección **"Estado de diferidos"** (salida de la fase 10, incluyendo el cierre de P2-10).
    - Lista **"Revisado y OK"** actualizada (heredando la del 06-10 y sumando lo verificado
      ahora), para que la próxima pasada sepa qué no re-mirar.
    - Prioridades: **P0** corrompe datos / rompe emisión / crash · **P1** bug funcional o
      riesgo real en producción · **P2** robustez / deuda importante · **P3** menor.

## Estrategia de modelos (abaratar la corrida)

Subagentes baratos **recolectan** hallazgos crudos; el modelo principal **verifica** todo
P0/P1 leyendo el código él mismo antes de que entre al informe (ningún hallazgo de subagente
entra sin verificar).

| Fase | Modelo | Por qué |
|---|---|---|
| 1. Línea base | haiku | Correr comandos y copiar resultados. |
| 2. Diff sin commitear | **principal** | Es el corazón de la pasada; requiere criterio. |
| 3. Regresión de fixes | sonnet (recolecta) + principal (verifica) | Cotejar fixing-log contra código es semi-mecánico; los dudosos los confirma el principal. |
| 4. Negocio y flujos | **principal** | Razonamiento sobre estados, plata y fallas a mitad de camino. |
| 5. AFIP (deltas) | **principal** | Crítico: errores acá rompen la facturación real. |
| 6. PDF | sonnet | Verificación mecánica contra template y modelo unificado. |
| 7. UI/MVVM | sonnet | Chequeo de patrones, volumen alto. |
| 8. Modelo de datos | **principal** | Contrasta contra negocio documentado. |
| 9. Cobertura de tests | sonnet | Inventario de cobertura semi-mecánico. |
| 10. Diferidos | **principal** | Clasificar requiere criterio de entrega. |
| 11. Entregabilidad | **principal** | Relevamiento con juicio de producto. |
| 12. Informe final | **principal** | Consolidación, deduplicación y priorización. |

Orquestación:
- Lanzar las fases de subagente en paralelo (independientes entre sí) según la tabla; cada
  una escribe su salida cruda en `doc/auditoria/raw/fase-NN.md` con formato
  `archivo:línea — hallazgo — evidencia`.
- Cada prompt de subagente debe ser autocontenido (qué buscar, qué excluir, dónde escribir)
  porque el subagente arranca sin el contexto de esta sesión.
- El modelo principal corre sus fases propias mientras esperan los subagentes, después
  verifica los crudos y arma el informe final. Borrar `doc/auditoria/raw/` al terminar.

## Reglas de ejecución

- Antes de las fases 4, 8 y 10, leer `doc/negocio/` completo y `doc/arquitectura/datos.md` +
  `flujos.md` para auditar contra lo documentado, no contra supuestos.
- **Solo lectura:** no editar código, no tocar git. El único output es el informe.
- Excluir `Afip.Net/Soap/Generated/**` de los análisis de estilo/calidad (código generado).
- Cada hallazgo debe ser accionable en frío: archivo:línea y fix concreto, sin depender del
  contexto de la sesión que lo encontró.
- No re-reportar como hallazgo nuevo lo que ya figura como diferido/aceptado en la fase 10 —
  esos van a su propia sección.
- Al final, actualizar la lista de lo revisado y OK (para saber qué no re-mirar la próxima).
