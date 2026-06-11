# Fixing Log — Auditoría 2026-06-10

> Ejecutado sobre `main` (rama `fix/auditoria-2026-06-10` no pudo crearse: git rechazado por permisos de la sesión).

| Ítem | Estado | Detalle |
|---|---|---|
| **P0-1** | ✅ hecho | Fix CP: eliminado `Empresa = empresa` en `CamaraPortuariaReciboService.EmitirOResumirAsync`. Test de regresión `EmitirMasivo_ConContextosSeparados_*` (2 nuevos) verde. Suite: 76/76. |
| **P1-1** | ✅ hecho | CM cierre Pendiente-first + `AddConVouchersAsync` atómico + `GetAgenciasConConsolidadoPendienteAsync` para reintento. Tests: `CerrarPeriodo_FallaCae_*` + `CerrarPeriodo_ReintentoTrasFalloCae_*`. Suite: 79/79. |
| **P1-2** | ✅ hecho | `AnularConNotaAsync` en ambos repos (CP+CM) + desvinculación de vouchers en CM. Servicios actualizados. Test `Anular_PersisteReciboYNotaJuntos_CM`. Suite: 79/79. |
| **P1-3** | ✅ hecho | `ExisteConsolidadoAsync` excluye Anulados; índice único CM filtro `AND "Estado" <> 'Anulado'`. Test `AnularConsolidado_PermiteReemitirElPeriodo`. Suite: 84/84. |
| **P1-4** | ✅ hecho | `FiltrarPorClave(grupoId:null)` retorna solo Pendiente; CM también excluye consolidados. Decisión D-20 en registro. Tests nuevos CP+CM. Suite: 84/84. |
| **P1-5** | ✅ hecho | `BackupService` ambas apps: `ClearAllPools()` en `RestaurarAsync` + `File.Delete` antes de VACUUM. Mensaje de éxito "Base restaurada. Cierre y vuelva a abrir la aplicación." en ambos ViewModels. |
| **P1-6** | ✅ hecho | `App.xaml.cs` ambas apps: `OnStartup` en try/catch; `CreateAsyncScope` en `InicializarBaseDeDatosAsync`; `OnExit.StopAsync` en try/catch. |
| **P1-7** | ✅ hecho | `ComprobanteTemplate.cs`: bloque condicional `ComprobanteAsociado` después de receptor. Test `GenerarPdf_NotaDeCreditoConComprobanteAsociado_DevuelvePdfValido`. Suite: 85/85. |
| **P1-8** | ✅ hecho | `IngresosBrutos`+`InicioActividades` en ambas entidades `Configuracion`; `HasMaxLength(50)` en Config; `AfipConfig` ampliado; providers actualizados; PdfService asigna al EmisorDocumento; UI con TextBox+DatePicker en tab Emisor. |
| **P1-9** | ✅ hecho | `appsettings.json` en ambas apps; `ModoDemo`/`Afip` → props estáticas; `ConfigurationBuilder` antes del Host; `#pragma CS0162` eliminados; banner "MODO DEMO" en MainWindow. Migración Inicial regenerada (85/85). |
| **P2-1** | ✅ hecho | `EmisionMasivaViewModel` (ambas): `_cargarCts` + cancel-previo en `CargarEstadoAsync`. |
| **P2-2** | ⏭️ diferido | Reconciliación `FECompConsultar` tras timeout: requiere diseño en Afip.Net. |
| **P2-3** | ✅ hecho | `AfipService.cs`: `FechaVencimientoCae ?? default` + `LogWarning` si null (no más `AddDays(10)` inventado). |
| **P2-4** | ✅ hecho | `TraBuilder.FirmarCms`: `EphemeralKeySet`; comentario SHA256 actualizado. |
| **P2-5** | ✅ hecho | Índice único `(PuntoDeVenta, NumeroComprobante, CodigoAfip)` con filtro `> 0` en ambas `ReciboConfiguration`; migración `Inicial` regenerada (93/93). |
| **P2-6** | ✅ hecho | `ControlPagosViewModel` (ambas): `SoloVencidos` setter dispara `BuscarAsync`. |
| **P2-7** | ✅ hecho | `VouchersPage.xaml`: `TextBox` de importe → `ui:NumberBox` (igual que GruposPage). |
| **P2-8** | ✅ hecho | `CuitValidator.cs` en `PuertoBB.Core/Common`; validación en `EmpresasViewModel` y `AgenciasViewModel`; 8 tests (3 válidos del SeedData, 5 inválidos). |
| **P2-9** | ✅ hecho | `BackupService.BackupAsync` (ambas): `File.Delete(destino)` previo si existe. (Entró junto con P1-5.) |
| **P2-10** | ⏭️ diferido | Datos reales en SeedData: decisión del usuario (anonimizar o externalizar). |
| **P2-11** | ✅ hecho | `MailConfigProvider` (ambas): `Unprotect` al leer; `ConfiguracionViewModel`: plaintext en memoria, `Protect`-guardar-restore en `GuardarCorreoAsync`. |
| **P2-12** | ✅ hecho | `doc/arquitectura/datos.md`, `flujos.md`, `doc/negocio/funcionalidad-compartida.md` actualizados al modelo real (Lineas, Receptor*, EmisionGrupo, Pendiente, PuntoDeVenta, etc.). |
| **P2-13** | ✅ hecho | `ReenviarMailAsync` (ambas): guard `if (string.IsNullOrEmpty(recibo.CAE)) return Fail(...)`. |
| **P2-14** | ✅ hecho | Guard `PuntoDeVentaActivo is null → Fail(...)` en los call-sites públicos de emisión y anulación (ambos servicios). |
| **P3-1** | ✅ hecho | `GetDuplicadosAsync` (ambos servicios): reemplazado N×`ExisteAsync` por `GetPorGrupoYPeriodoAsync` + HashSet. |
| **P3-2** | ✅ hecho | `EnviarMasivoAsync` (ambos servicios): query batch con `GetPorGrupoYPeriodoAsync` (incluye `Empresa/Agencia.Emails` + `Lineas`). Removido `AsNoTracking` de la query para evitar conflicto de identidad en tests (93/93). |
| **P3-9** | ✅ hecho | `RecibosViewModel` (ambas): `await GuardarConceptosAsync(...)` en lugar de fire-and-forget. |
| **P3-10** | ✅ hecho | `RecibosViewModel`/`ControlPagosViewModel` (ambas): reemplazo de colección en `AplicarFiltro` (1 evento vs N). |
| **P3-11** | ✅ hecho | `RecibosPage.xaml`/`ControlPagosPage.xaml` (ambas): `VirtualizingPanel.VirtualizationMode="Recycling"`. |
| **P3-12** | ✅ hecho | `ConfiguracionPage.xaml.cs` (ambas): `Unloaded` desuscribe `PropertyChanged`. |
| **P3-15** | ⏭️ diferido | Portar visor PDF embebido de CM a CP: requiere trabajo de UI no trivial. |
| **P3-16** | ✅ hecho | `GruposViewModel` CM: mensaje de error al eliminar incluye hint "¿tiene recibos asociados?". |
| **P3-18** | ✅ hecho | `ComprobanteTemplate.cs`: totales del pie unificados a `FormatMoneda` (C2, sin `$` en label). |
