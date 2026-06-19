# Auditoría pre-producción — PuertoBB

> Informe único que reemplaza a los informes/fixing-logs anteriores (06-10 y 06-11, borrados).
> Fecha: 2026-06-17. Alcance: solución completa (CP + CM + Afip.Net + Services + Infrastructure),
> con foco en integridad de datos, persistencia, consistencia de flujos, correo/OAuth, seguridad y robustez.

## 1. Metodología y disciplina de triage

Cada hallazgo se clasificó **leyendo el código** antes de proponer un cambio (la exploración con sub-agentes
produjo varios "críticos" que resultaron falsos positivos). Clasificación: **Real / Falso positivo / Decisión**,
y si es Real: **Bloqueante / Recomendado / Bajo / Diferible**.

## 2. Línea base

- **Build:** `dotnet build PuertoBB.slnx` → al inicio 4 warnings `CS8604` (en los loops de emisión masiva) + 1 `CS0618`
  latente en `PdfMultipaginaTests`. Tras las correcciones: **0 warnings, 0 errores**.
- **Tests:** baseline 242 verdes (20 `Afip.Documentos.Tests` + 222 `PuertoBB.Tests`). Tras las correcciones: **243 verdes**, 0 fallidos.

## 3. Falsos positivos / decisiones vigentes (NO tocar)

| Tema | Veredicto | Evidencia |
|---|---|---|
| `Importe` como `TEXT` en SQLite | Correcto y **lossless** (NUMERIC/REAL introduciría error de punto flotante) | `ReciboConfiguration.cs:12` |
| Validación Tenant ID Microsoft "faltante" | **Ya existe** | `MailConfig.cs:71-72` |
| Cascada `Recibo→EmisionGrupo` | **Intencional** (recibos inmutables sobreviven al borrado del grupo) | `EmisionGrupoConfiguration.cs:20-29` |
| Secretos en texto plano (SMTP/OAuth/certificado) | **Decisión D-24** — riesgo residual aceptado | `Configuracion.cs:34,47` |
| Guard de recuperación de CAE distinto CP/CM | Estructural, no bug (CP corta antes con `return`) | CP `:275-280` |
| Flujos multi-fase sin transacción explícita | **Idempotente por diseño** ("Pendiente-first", reanudable) | servicios de negocio |
| PDF regenerado a demanda (no persistido) | **Decisión** documentada | `doc/arquitectura/datos.md` |
| `OAuthTokenProvider` Singleton "captura transient" | Falso: solo inyecta `ILogger`; `MailConfig` por parámetro | `OAuthTokenProvider.cs:15` |
| Logging de secretos / path traversal en backup / OAuth loopback / ModoDemo | Auditados **limpios** | ver §6 |
| Cast a `double` en mapper SOAP | Redondeado AwayFromZero antes del cast, documentado (P3-7) | `WsfeMapper.cs:17` |

## 4. Correcciones aplicadas

| ID | Severidad | Descripción | Archivos | Estado |
|---|---|---|---|---|
| **Warnings** | — | `CS8604` (guarda de no-nulabilidad en loops masivos) + `CS0618` (`PdfDocumentOpenMode.Import`) → **0 warnings** | `CamaraPortuariaReciboService.cs`, `CentroMaritimoReciboService.cs`, `PdfMultipaginaTests.cs` | ✅ |
| **O1/C2** | Bloqueante | Clave de cache OAuth: `secreto?.GetHashCode()` → **SHA-256 estable** (`HashSecreto`) | `OAuthTokenProvider.cs` | ✅ |
| **C1** | Bloqueante | Carga inicial fire-and-forget sin captura (~9 ViewModels × 2 apps + setters): helper común `CargarSeguro` en `PageViewModel` (try/catch + `MostrarError`); reemplazo de todos los `_ = XxxAsync()` | `*/ViewModels/Base/PageViewModel.cs` + ViewModels de ambas apps | ✅ |
| **C3** | Recomendado | `ReintentarAsync` re-sincroniza el snapshot del receptor si el recibo sigue sin CAE (destraba Pendientes por RG 5616) + test | `Camara/CentroMaritimoReciboService.cs`, `ServiceFlowTests.cs` | ✅ |
| **C4** | Recomendado | Traza del mail de la NC: se persiste en `FechaEnvioMail`/`UltimoErrorMail` (anulación y reenvío); `EtiquetaEnvio` muestra el envío de la NC para Anulado | servicios CP/CM, `EstadoReciboHelper.cs`, `HelpersTests.cs` | ✅ |
| **C5** | Bajo | Default `EstadoFiscal.Pendiente` en `ConstruirRecibo` (evita "Emitido sin CAE") | `CentroMaritimoReciboService.cs:506` | ✅ |
| **C6** | Recomendado | Chequeo de certificado AFIP vencido/por vencer en "Probar conexión" (`TraBuilder.CargarCertificado` + `NotAfter`/`NotBefore`) | `Afip.Net/Wsaa/TraBuilder.cs`, `AfipService.cs` | ✅ |
| **C9** | Bajo | Color por defecto visible (`#ECEFF1`) para estados no mapeados | `EstadoReciboToBrushConverter.cs` (CP/CM) | ✅ |
| **C10** | Bajo | Overload de `PageViewModel.EjecutarConProgresoAsync` ya unificado entre apps (alineada diferencia cosmética) | `PageViewModel.cs` (CP/CM) | ✅ |

## 5. Pendientes / decisiones abiertas

| ID | Severidad | Estado | Nota |
|---|---|---|---|
| **C0** | Bloqueante | **Resuelto** | `StackOverflowException` corregido por el usuario. |
| **C8** | Recomendado | **Resuelto** | Decisión del usuario: el CUIT no debería repetirse **pero se permiten excepciones** → **validación blanda** (aviso de CUIT duplicado con confirmación en el alta/edición de Empresas y Agencias), **sin índice único duro ni migración**. |
| **C12** | Bajo/Opcional | **Parcial** | ✅ Timeout SOAP explícito (60 s) en `WsfeSoapClient`/`WsaaSoapClient`. ⏳ Aviso de ambiente más visible: pendiente (toca `MainWindow`/shell WPF — decisión de UX). |
| **C13** | Diferible | No bloqueante | Transacción explícita en flujos multi-fase: el patrón "Pendiente-first" ya es idempotente/recuperable. |

## 6. Seguridad y robustez (baseline positivo)

- **Secretos en logs:** no se loguean tokens/refresh/client secret/contraseña SMTP/certificado/CAE completo.
- **Backup/restore:** ruta vía diálogo; `VACUUM INTO` con escape de comillas; sin SQL crudo concatenado con input.
- **OAuth loopback:** valida `state`, puerto efímero, `id_token` se parsea solo para mostrar el email (no autoriza).
- **ModoDemo / MailReal / Afip:** defaults seguros (Mock/Fake); no hay ambigüedad que golpee AFIP/correo real por error.
- **Certificado AFIP:** nunca se escribe a disco temporal; se carga en memoria.
- **Cultura es-AR** fijada globalmente; `decimal` en todo el pipeline (`double` solo en marshalling SOAP, redondeado antes).
- **QR AFIP** conforme a ARCA (testeado); PDF multipágina testeado; ticket WSAA con cache thread-safe + barrido de vencidos.
- **Disposal** correcto (`SmtpClient` en `using`, `HttpClient` estático, `IHost.StopAsync`, logger); `Nullable` enable sin supresiones.

## 7. Cobertura de tests

Cobertura existente sólida (no había que crearla): `MailConfig.Validar()` (todas las ramas), `OAuthPresets`, `MailErrores`,
`CuentaCorreo` (seed/activa/round-trip), emisión individual/masiva, anulación con NC, reintento, recuperación
anti-duplicado de CAE, contextos separados (P0-1), `CerrarPeriodoAsync` (CM). **Sumado en esta auditoría:** test de C3
(reintento que destraba por RG 5616). Total: **243 verdes**.

## 8. Checklist de entregabilidad a producción

- [x] Build 0 warnings / 0 errores.
- [x] Suite verde (243).
- [x] **C0**: `StackOverflowException` resuelto por el usuario.
- [ ] **C8**: decisión de negocio sobre unicidad de CUIT.
- [ ] Smoke manual de ambas apps (arranque, Configuración → Correo con OAuth real, emisión, anulación).
- [ ] Prueba real AFIP en **homologación** con el certificado del usuario.
- [ ] Prueba real de **SMTP/OAuth** con proveedor real (Microsoft 365 / Google) — resuelve el `535 5.7.139`.
- [ ] Commit del working tree (queda sin commitear a propósito, para revisión del usuario).
