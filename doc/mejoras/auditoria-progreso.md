# Progreso — Auditoría + Corrección nocturna

> Estado vivo del trabajo desatendido. El loop lee y actualiza este archivo en cada iteración.
> **Regla:** sin commits de git. Build verde + tests verdes tras cada lote.

## Estado global

- Inicio: 2026-06-08 (sesión nocturna)
- Fase actual: **L-Z VALIDADO — loop pausado en estado verde, esperando decisiones del usuario para lo estructural**
- Baseline 2026-06-08: Build ✅ 0/0 | Tests ✅ 63 | Migraciones a verificar
- **Cierre L-Z (iter 28): Build ✅ 0/0 · Tests ✅ 63 (47+16) · Migraciones ✅ en sync (CP+CM) · Sin Console/Debug.WriteLine · 0 commits.**

## RESUMEN FINAL (para el usuario)

### Aplicado y validado (build+tests verdes, sin commitear)
- **Pulido visual completo:** ToolTips homogéneos en TODOS los botones de operación de ambas apps (operación + CRUD + Configuración). Iconos unificados (F-17 Emitir→Receipt24 / "Emitir y enviar"→Send24; F-18 Previsualizar→DocumentPdf24).
- **Bugs de negocio corregidos:** F-09 (MarcarPagado rechaza Anulado/Pendiente), F-10 (Anular exige CAE), F-12 (AfipService valida CUIT con TryParse). En ambas apps.
- **Documentación canónica nueva:** `doc/diseño/design-system.md` (catálogo operación→icono→tooltip), `doc/arquitectura/receta-entidad-end-to-end.md` (Definition of Done), `doc/mejoras/informe-auditoria.md` (hallazgos F-01..F-18 + HK).
- **Skills/guardrails:** `/nueva-seccion`, `/no-tocar-git` + deny de git en `.claude/settings.local.json`.

### Pendiente — requiere tu decisión (NO se aplicó a ciegas)
- **Estructural HK-1..HK-4** (ítems del recibo + snapshot + NC): diseño completo en `doc/mejoras/diseno-recibo-items.md`. Implica migración EF + backfill → revisá y dá OK; luego se ejecuta L-D/L-E.
- **F-06** (SmtpPassword en texto plano): ¿cifrar con DPAPI o documentar?
- **F-02** (Detalle maxlen 1000/2000): se difiere para bundlear con HK-3.
- Tests de las guardas F-09/F-10 (L-G), pendientes de agregar.

## FASE 1 — Auditoría

- [ ] Ejecutar `doc/mejoras/prompt-auditoria-integral.md` (pasos 0–9 + HK-1..HK-4)
- [ ] Informe en `doc/mejoras/informe-auditoria.md`
- [ ] Borradores de docs/skills en `doc/mejoras/borradores/`
- [ ] Definir lotes de corrección concretos (abajo)

## FASE 2 — Corrección (aplicar TODO, sin commitear)

- [~] **L-A/L-B** Quick wins + bugs de negocio — F-09 (MarcarPagado guard) ✅, F-10 (Anular exige CAE) ✅ en ambas apps; pendiente F-12 (TryParse CUIT), F-02 (Detalle maxlen), código muerto/format
- [ ] **L-B** Homogeneización CP↔CM no estructural + paridad funcional CM→CP
- [~] **L-C** Consistencia visual / pulido — **TOOLTIPS COMPLETOS ✅** (operación + CRUD + Configuración, ambas apps; Dashboard/Editar/Guardar/Cancelar excluidos por decisión). Pendiente: unificar iconos (F-17 Emitir, F-18 Previsualizar), estados vacío/carga, formato es-AR, tema
- [x] **L-D** Estructural HK-1/2/3 — Core `ReciboLinea` (CP+CM) ✅, EF config ✅, **migración + backfill CP+CM** ✅, emisión puebla `Lineas` ✅, PDF lee del snapshot ✅, **editor multi-ítem en la UI de emisión individual (CP+CM)** ✅ (Agregar/Quitar ítem + total + DataGrid; fallback a ítem único). `EmitirIndividualAsync` con parámetro opcional `lineas`. Smoke test runtime OK (migración aplica, 3 recibos↔3 líneas). Build full 0/0 + 48 tests.
- [x] **L-E** Estructural HK-4 — NC renderiza el detalle (líneas) del recibo original (anulación total). PDF de NC CP+CM ✅. Build+tests verdes.
- [~] **L-F** Documentación canónica + skills (Paso 9) — `doc/diseño/design-system.md` ✅, `doc/arquitectura/receta-entidad-end-to-end.md` ✅, skill `/nueva-seccion` ✅; pendiente: glosario negocio (opcional), reforzar skills `desarrollador`/`arquitecto` para que lean el estándar
- [ ] **L-G** Tests de los huecos detectados
- [ ] **L-Z** Cierre: `/validar-plataforma` completo + resumen final

## Bitácora (más reciente arriba)

- **Iter 32 (2026-06-08):** Cierre de findings opcionales + auditoría fina. F-15 (acento PDF→PdfTheme) ✅; F-11 parte segura (EsCompleto→EstadoReciboHelper compartido) ✅, dedup AFIP NC se deja por riesgo; TODO VoucherService→nota de decisión ✅. Paso 5 (Afip.Net/Mock/Documentos) auditado OK (TraBuilder/TicketCache/QR/Mapper sólidos; nota: comentario SHA1 stale). Paso 7: +2 tests de guardas (F-09/F-10) → **50 tests verdes**. Paso 8 transversal revisado OK. Build full 0/0.

- **Iter 31 (2026-06-08):** Validación full con apps cerradas: build solución 0/0, 48 tests verdes, migraciones en sync (CP+CM, ahora con AgregarReciboLineas + UnificarLargoDetalleRecibo). Smoke test CP OK (arranca sin errores, migraciones aplicadas, cultura es-AR). F-05/F-06/F-02/F-19 cerrados. Abiertos (opcionales/bajo): F-11 (DRY services), F-15 (acento PDF→PdfTheme), TODO VoucherService:138, auditoría fina Paso 5/7/8.

- **Iter 30 (2026-06-08):** F-19 (cultura es-AR) — `ConfigurarCultura()` en App.xaml.cs de CP+CM (DefaultThreadCurrentCulture + FrameworkElement.Language) → grillas XAML y `Formato` consistentes. CP.UI + no-UI compilan 0/0; CM.UI quedó sin copiar por instancia abierta (no es error de código). F-02 (Detalle maxlen) — CP 1000→2000 unificado con CM + migración `UnificarLargoDetalleRecibo`, en sync. Falta: build full con CM cerrado; decisión F-06 (SMTP).

- **Iter 29 (2026-06-08, con el usuario):** COMPLETADO el editor multi-ítem (HK-2 UI) en CP+CM: `EmitirIndividualAsync` con `lineas` opcional (aditivo, tests intactos); VM con `LineasEmision`/`TotalEmision`/Agregar/Quitar; XAML con DataGrid de ítems + total (Visibility por `HayLineas`); fallback a ítem único. **Build full solución 0/0, 48 tests verdes, migraciones en sync.** Smoke test CM runtime OK: arranca sin errores, migración aplicada, 3 recibos↔3 líneas con detalle correcto. ✅ HK-1/HK-2/HK-3/HK-4 IMPLEMENTADOS Y VALIDADOS.

- **Iter 28+ (2026-06-08, con el usuario despierto):** IMPLEMENTADO lo estructural (no era solo diseño):
  - Core: `ReciboLinea` (CP+CM) + `Recibo.Lineas`. (Renombrado de "Item" a "Linea" por choque con el VM de UI `ReciboItem`.)
  - Infra: `ReciboLineaConfiguration` (FK+índice+cascade), `DbSet<ReciboLinea>`, **migración `AgregarReciboLineas` CP+CM con backfill** (1 línea por recibo histórico con su Detalle+Importe). Migraciones en sync.
  - Services: emisión individual/masiva crea 1 línea (detalle+importe); cierre CM crea **1 línea por voucher**; total = suma.
  - PDF: CP y CM (simple, consolidado y **NC**) ahora renderizan SIEMPRE desde `recibo.Lineas` (HK-1 resuelto: mismo detalle desde cualquier pantalla). `GetConDetalleAsync` incluye `Lineas`.
  - Tests: +1 (snapshot total=suma). Total 48 verdes. Services/Infra/Tests build 0/0.
  - **NOTA: build de UI bloqueado** (app CentroMaritimo PID 54012 + VS abiertos). Falta el editor multi-ítem en la UI de emisión individual (HK-2 UI) y validar build full UI.
- **Iter 27 (2026-06-08):** Paso 9 — creado skill `.claude/commands/nueva-seccion.md` (`/nueva-seccion`): carga receta + convenciones + design-system, exige paridad CP↔CM, iconos/tooltips del catálogo y Definition of Done; incluye "no commitear". Sigue: reforzar skills desarrollador/arquitecto para apuntar al estándar; luego L-Z validación global de cierre.
- **Iter 26 (2026-06-08):** Paso 9 — escrito `doc/arquitectura/receta-entidad-end-to-end.md`: pasos Core→EF/migración→Services→DI→UI(ambas apps)→Tests→Docs + Definition of Done (checklist build/tests/migraciones/paridad/iconos+tooltips). Sigue: evaluar skills nuevos (scaffolding CRUD que use la receta; design-system) y/o reforzar skills existentes; luego validación global L-Z.
- **Iter 25 (2026-06-08):** Paso 9 — escrito `doc/diseño/design-system.md`: acentos por app, estilo AccionIconButton, catálogo canónico operación→icono→tooltip (única fuente), patrón de página, regla anti-MessageBox, estados de UI, formato es-AR. Sigue: receta de implementación "agregar entidad end-to-end" (doc/arquitectura) y evaluar skills nuevos (scaffolding CRUD, design-system).
- **Iter 24 (2026-06-08):** Escrito design doc estructural `doc/mejoras/diseno-recibo-items.md` (HK-1/2/3/4): entidad ReciboItem, snapshot inmutable, NC con snapshot (anulación total), migración CP+CM con backfill, cambios por capa, orden de implementación y preguntas para el usuario. Listo para revisión/OK. Sigue: Paso 9 — documentación canónica + skills (seguro, pedido por el usuario).
- **Iter 23 (2026-06-08):** F-12 corregido (AfipService valida CUIT receptor/asociado con TryParse → error claro; ToAfipRequest defensivo). Build + 47 tests verdes. DECISIÓN: lo estructural (HK-1..HK-4) requiere migraciones EF + cambios AFIP/PDF/UI = alto riesgo 100% desatendido → priorizar (a) design doc de HK-1..HK-4 para revisión del usuario, (b) Paso 9 docs/skills (seguro, pedido por el usuario). F-02 (Detalle maxlen) se difiere para bundlear con HK-3 (cambia el modelo). Sigue: escribir doc de diseño estructural + arrancar docs canónicas.
- **Iter 22 (2026-06-08):** Bugs de negocio (seguros) — F-09 (MarcarPagado rechaza Anulado/Pendiente) y F-10 (Anular exige CAE) corregidos en CamaraPortuaria + CentroMaritimo ReciboService. Build solución verde 0/0 + 47 tests verdes. Falta agregar tests de estas guardas (L-G). Sigue: F-12 (TryParse CUIT en AfipService), F-02 (unificar Detalle maxlen 1000/2000), luego planificar estructural HK-1..HK-4 o Paso 9 docs.
- **Iter 21 (2026-06-08):** L-C iconos — F-17 resuelto (EmisiónMasiva CP+CM: "Emitir"→"Emitir y enviar", Send24) y F-18 resuelto (Previsualizar→DocumentPdf24 en Vouchers+CierrePeriodo CM). Build solución verde 0/0. Iconografía homogénea: Receipt24=Emitir, Send24=Emitir y enviar, DocumentPdf24=Previsualizar PDF. Sigue: estados vacío/carga (revisión), formato es-AR, y empezar a planificar lo estructural (L-D/L-E) o cerrar pulido + Paso 9 docs.
- **Iter 20 (2026-06-08):** L-C tooltips — ConfiguracionPage CP (paridad: PV, backup/restaurar, verificar, probar conexión AFIP/mail). Build solución verde 0/0 + 47 tests verdes. ✅ HITO: TOOLTIPS COMPLETOS en ambas apps (operación + CRUD + Configuración). Sigue: unificar iconos F-17 (Emitir→Receipt24) y F-18 (Previsualizar→DocumentPdf24), después estados vacío/carga y formato es-AR.
- **Iter 19 (2026-06-08):** L-C tooltips — ConfiguracionPage CM: Generar/Restaurar backup, Verificar integridad, Marcar activo, Nuevo/Eliminar PV, Probar conexión (AFIP) y Probar conexión (mail). Compactar/Optimizar ya tenían. Build CM verde. DECISIÓN: Editar/Guardar/Cancelar por sección quedan sin tooltip (texto+contexto claros). Dashboard = tiles de navegación con texto (sin tooltip). Sigue: paridad en ConfiguracionPage CP, luego unificar iconos F-17 (Emitir) y F-18 (Previsualizar).
- **Iter 18 (2026-06-08):** L-C tooltips — CP CRUD: EmpresasPage + ConceptosReciboPage + GruposPage (Nuevo/Guardar/Eliminar). Build CP verde. Ambas apps cubiertas en operación + CRUD. Sigue: Dashboard + Configuración (revisión de botones, p.ej. Backup/Probar conexión/Agregar PV), luego unificar iconos F-17/F-18 y validación global.
- **Iter 17 (2026-06-08):** L-C tooltips — AgenciasPage + BarcosPage (CM). Build CM verde. CM cubierto en operación + CRUD (resta Dashboard/Configuración CM, revisión rápida). Sigue: paridad CRUD en CP (EmpresasPage, ConceptosReciboPage, GruposPage) + Dashboard/Configuración ambas apps.
- **Iter 16 (2026-06-08):** L-C tooltips — CRUD CM: ConceptosReciboPage + GruposPage (Nuevo/Guardar/Eliminar). Build CM verde. Patrón Nuevo=Add24/Guardar=Save24/Eliminar=Delete24 consistente. Sigue: AgenciasPage, BarcosPage (CM) y luego EmpresasPage/Conceptos/Grupos CP (paridad) + Dashboard/Configuración.
- **Iter 15 (2026-06-08):** L-C tooltips — CierrePeriodoPage CM: faltaba solo "Refrescar" (resto ya tenía, incl. botones por fila). Build CM verde. ✅ HITO: las 5 páginas de operación que pidió el usuario (Vouchers, Cierre de período, Recibos, Emisión masiva, Control de pagos) ya tienen tooltips homogéneos. Sigue: páginas CRUD (Grupos/Conceptos/Agencias/Empresas/Barcos), Dashboard, Configuración; después unificar iconos F-17/F-18.
- **Iter 14 (2026-06-08):** L-C tooltips — VouchersPage CM: Buscar/Eliminar/Editar/Cancelar/Guardar (Previsualizar/Descargar/Ir-a-Agencias ya tenían). Build CM verde. Nuevo F-18 (🟡 Previsualizar Eye24 vs DocumentPdf24). Catálogo iconos +Delete24/Edit24/Dismiss24/Save24/Add24. Sigue: CierrePeriodoPage CM, luego CRUD (Grupos/Conceptos/Agencias/Empresas/Barcos) y Dashboard/Configuración.
- **Iter 13 (2026-06-08):** L-C tooltips — RecibosPage CP (idéntica a CM): mismos 4 botones completados. Build verde 0/0. Sigue: VouchersPage + CierrePeriodoPage (CM), luego páginas CRUD (Grupos/Conceptos/Agencias/Empresas/Barcos) y Dashboard/Configuración.
- **Iter 12 (2026-06-08):** L-C tooltips — RecibosPage CM: agregados a Buscar/Anular/Reenviar/Marcar pagado (Reintentar/Previsualizar/Emitir/Emitir y enviar ya tenían). Build verde. Catálogo de iconos canónico capturado. Nuevo F-17 (🟡 icono Emitir: Receipt24 vs Send24 inconsistente). Sigue: RecibosPage CP (paridad), luego Vouchers/CierrePeriodo/Grupos/Conceptos/Agencias/Barcos/Dashboard/Configuración.
- **Iter 11 (2026-06-08):** L-C tooltips — agregados a EmisionMasivaPage CP+CM (botón Emitir). Build verde 0/0. Sigue: RecibosPage (botones por fila + barra), Vouchers, CierrePeriodo, y resto.
- **Iter 10 (2026-06-08) [FASE 2 inicio]:** Empezado L-C (tooltips, F-16). Aplicados ToolTips a ControlPagosPage CP+CM (Actualizar / Marcar pagado / Reenviar mail). Build verde 0/0. Pendiente tooltips en: Recibos, EmisiónMasiva, Vouchers, CierrePeriodo, Grupos, Conceptos, Agencias/Empresas, Barcos, Dashboard, Configuración (ambas apps). NOTA: FASE 1 quedó ~80% (faltan Paso 5 Afip.Net/Documentos, resto de UI/MVVM/XAML, Paso 7 tests, Paso 8 transversal, Paso 9 docs/skills) — se completa intercalada con FASE 2.
- **Iter 9 (2026-06-08):** Paso 6.bis (iconos/tooltips) iniciado. Buena base: estilo compartido AccionIconButton + iconos semánticos; ControlPagosPage byte-idéntica CP/CM. Nuevo F-16 (🟡 botones de operación sin ToolTip — pedido del usuario). Iniciado catálogo operación→icono→tooltip. Sigue: revisar botones por fila (Recibos/Vouchers/CierrePeriodo, probables icon-only), Paso 5 (Afip.Net/Documentos), y luego PIVOTAR a FASE 2 (quick wins L-A + tooltips L-C).
- **Iter 8 (2026-06-08):** Paso 4 cerrado (esencial). PdfService CP vs CM: HK-1 confirmado en capa PDF (CM arma Items de vouchers e ignora Detalle; PDF simple usa Detalle). Nuevos: F-13 (🟠 CP PDF sin ítems), F-14 (🟡 NC PDF sin ítems), F-15 (🟢 acento hex hardcodeado vs PdfTheme). Sigue: Paso 5 (Afip.Net/Mock/Documentos) y Paso 6 (UI: DI, MVVM, XAML, iconos/tooltips, paridad CM→CP).
- **Iter 7 (2026-06-08):** Paso 4 — AfipService auditado: limpio (adaptador, ServiceResult, logging, diagnóstico). Nuevo F-12 (🟡 long.Parse de CUIT puede tirar FormatException → mensaje genérico; usar TryParse). Sigue: los dos PdfService (confirmar HK-1 en PDF + paridad), VoucherService, Formato/PeriodoHelper, y cerrar Paso 4.
- **Iter 6 (2026-06-08):** Paso 4 — comparado CentroMaritimoReciboService vs CP. Buena paridad estructural. HK-1 causa raíz confirmada (CM:129 detalle="Vouchers Nros:..." vs PDF:159 que lo ignora). F-09/F-10 también en CM (bugs compartidos). F-11 (🟡 DRY: AnularRecibo/request AFIP/EsCompleto duplicados). Sigue: VoucherService, AfipService, los dos PdfService, Formato/PeriodoHelper.
- **Iter 5 (2026-06-08):** Paso 4 (Services) — leído CamaraPortuariaReciboService. Manejo de errores/logging/idempotencia correctos. AnularReciboAsync ya es anulación total (alineado con decisión). Nuevos: F-09 (🟠 MarcarPagado sin guarda de estado), F-10 (🟠 Anular sin exigir CAE → NC contra comprobante 0). Refuerza F-04. Sigue: comparar CentroMaritimoReciboService (paridad + consolidación vouchers), VoucherService, AfipService, PdfService.
- **Iter 4 (2026-06-08):** Paso 3 (Infrastructure). RepositoryBase y repos CP↔CM correctos (AsNoTracking, DbUpdateException→ReciboException, Includes explícitos). F-07 (🟢 decimal sin precisión explícita pero OK en SQLite/TEXT), F-08 (🟢 DateTime.Now vs UtcNow). Pendiente fino: índices únicos/parciales y DeleteBehavior en *Configuration. Sigue: terminar Paso 3 (índices) y Paso 4 (Services: ServiceResult/errores/logging, comparar los dos ReciboService + PdfService, AFIP).
- **Iter 3 (2026-06-08):** Paso 2 (Core) — comparación entidades CP↔CM. Alta homogeneidad: ConceptoRecibo/GrupoFacturacion/PuntoDeVenta idénticos (salvo navegación de dominio); Configuracion difiere solo por apoderado+voucher (propio de CM). Nuevo hallazgo F-06 (🟡): SmtpPassword en texto plano vs CertificadoPassword cifrado (DPAPI). Pendiente fino: Empresa/Agencia/Barco, enums, DTOs Models/. Sigue: terminar Paso 2 y Paso 3 (Infrastructure: índices, precisión decimal, AsNoTracking, N+1, repos CP↔CM).
- **Iter 2 (2026-06-08):** Paso 1 (reglas de arquitectura) completado. OK: MessageBox solo en App.xaml.cs, sin Console/Debug.WriteLine, VMs sin DbContext. Nuevo hallazgo F-05 (🟠): try/catch dentro de ViewModels (CM 12 / CP 9 archivos), contra convención — a verificar caso por caso en Paso 4/6. Sigue: Paso 2 (Core: comparación entidades CP↔CM, enums, DTOs).
- **Iter 1 (2026-06-08):** Baseline verde (build 0/0, 63 tests). Migraciones EF en sync (CP/CM). Iniciado `informe-auditoria.md` con 4 hallazgos confirmados con evidencia: F-01 (HK-1, `CentroMaritimoPdfService.cs:159` deriva detalle de vouchers), F-02 (`Detalle` maxlen CP=1000≠CM=2000), F-03 (HK-2 recibo de un solo ítem), F-04 (HK-4 NC sin snapshot). Sigue: completar pasos 1–9 de la auditoría.

## Bloqueantes / decisiones pendientes para el usuario

- **Estructural HK-1..HK-4 (ítems del recibo + snapshot + NC):** implica una **migración EF nueva en CP y CM con backfill de datos históricos** + cambios en servicio de emisión, PDF y mapeo AFIP. Es el cambio más riesgoso para hacer 100% desatendido (puede dejar el esquema o datos en estado delicado). Por eso se deja como **documento de diseño para tu revisión** (`doc/mejoras/diseno-recibo-items.md` ✅ escrito) antes de aplicarlo. Decisión recomendada: revisarlo y dar OK por la mañana, y recién ahí ejecutar L-D/L-E. (NC = solo anulación total ya está fijado.)
- **F-06 (SmtpPassword en texto plano):** decisión tuya — ¿cifrar con DPAPI como el cert, o documentar que queda en claro? No lo toco sin tu OK.
