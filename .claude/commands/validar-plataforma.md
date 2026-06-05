Sos el validador de la plataforma PuertoBB. Ejecutá una validación integral de punta a punta y reportá un resumen claro (✅/❌ por sección). No corrijas nada salvo que el usuario lo pida: primero diagnosticá.

## 1. Build de toda la solución

```
dotnet build PuertoBB.slnx -clp:ErrorsOnly
```
Debe terminar en **0 errores y 0 advertencias**. Si hay advertencias nuevas, listalas.

## 2. Tests

```
dotnet test PuertoBB.Tests/PuertoBB.Tests.csproj --nologo
```
Reportá `Superado / Con error / Total`. Si algo falla, mostrá el nombre del test y el mensaje.

## 3. Migraciones EF

Verificá que el modelo está en sync con las migraciones (no faltan migraciones por generar):
```
$env:PATH += ";$HOME\.dotnet\tools"
dotnet ef migrations has-pending-model-changes --project PuertoBB.Infrastructure --context CamaraPortuariaDbContext
dotnet ef migrations has-pending-model-changes --project PuertoBB.Infrastructure --context CentroMaritimoDbContext
```
Si hay cambios pendientes, avisá que falta `dotnet ef migrations add`.

## 4. Smoke test de runtime de ambas apps

Para cada app (`CamaraPortuaria.UI`, `CentroMaritimo.UI`):
1. Borrá la base de demo: `Remove-Item "$env:LOCALAPPDATA\PuertoBB\<App>" -Recurse -Force` (recrea schema + seed limpios).
2. Lanzá la app en background con `dotnet run --project <App>/<App>.csproj`.
3. Esperá ~20s. Verificá que el proceso `<App>.exe` está vivo (`tasklist`).
4. Revisá el último log en `%LOCALAPPDATA%\PuertoBB\<App>\Logs\app-*.log` buscando `[ERR]`, `[FTL]` o `Exception` (ignorando líneas de `Executed DbCommand`).
5. Verificá que se creó la base `.db`.
6. Cerrá la app: `taskkill /IM <App>.exe /F`.

La app no debe crashear ni registrar errores/fatales en el arranque.

## 5. Reglas de arquitectura (chequeo estático)

Verificá con búsquedas (Grep) que **no** se violen estas reglas:

☐ **Core sin dependencias**: `PuertoBB.Core.csproj` no tiene `<ProjectReference>` ni `<PackageReference>`.
☐ **Services no referencia Infrastructure** ni viceversa (revisar los dos `.csproj`).
☐ **Sin `MessageBox` directo** fuera del handler de último recurso de `App.xaml.cs`: `Grep "MessageBox" --glob "*.cs"`.
☐ **Sin `Console.WriteLine` / `Debug.WriteLine`**: `Grep "Console.WriteLine|Debug.WriteLine" --glob "*.cs"`.
☐ **Sin colores hex hardcodeados en XAML de páginas** (deben venir de `Colors.xaml`/Fluent), salvo overlays `#80000000` de carga.
☐ **ViewModels sin `DbContext`**: `Grep "DbContext" --glob "*ViewModel*.cs"` (no debe haber resultados).
☐ **Servicios devuelven `ServiceResult`**: revisar que los métodos públicos de `Negocio/*Service.cs` no propaguen excepciones de negocio.

## 6. Reporte final

Resumí en una tabla: Build / Tests / Migraciones / Smoke CP / Smoke CM / Reglas. Marcá ✅ o ❌ y, si hay ❌, la causa concreta y el archivo/línea. No inventes: si no pudiste ejecutar algo (p. ej. falta display para WPF), decilo explícitamente.
