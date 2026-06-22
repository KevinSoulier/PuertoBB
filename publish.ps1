#requires -Version 5.1
<#
.SYNOPSIS
  Publica las dos apps WPF de PuertoBB como autoejecutables single-file (un .exe por app).

.DESCRIPTION
  Genera, por app, un único .exe framework-dependent en dist\<App>\:
    - PublishSingleFile + IncludeNativeLibrariesForSelfExtract → las DLLs nativas
      (SQLite e_sqlite3.dll, WebView2Loader.dll) quedan dentro del .exe.
    - appsettings.json NO se incluye (CopyToPublishDirectory=Never en el .csproj):
      el .exe corre en producción por los defaults (ambos mocks en false).

  Requisito en la PC destino: .NET 10 Desktop Runtime (x64) y WebView2 Runtime (Evergreen,
  ya viene en Windows 11). Detalle: doc\usuario\paso-a-produccion.md

.NOTES
  Para un .exe autónomo que no requiera el runtime instalado, cambiar --self-contained a true
  (el .exe pasa a pesar ~80-150 MB).
#>
$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

$rid  = 'win-x64'
$dist = Join-Path $PSScriptRoot 'dist'

$common = @(
  '-c', 'Release',
  '-r', $rid,
  '--self-contained', 'false',
  '-p:PublishSingleFile=true',
  '-p:IncludeAllContentForSelfExtract=true'       # mete TODO adentro del exe: nativas
                                                  # (e_sqlite3.dll, WebView2Loader.dll), la
                                                  # fuente Lato de QuestPDF y los .xml de doc.
                                                  # Los .pdb no aparecen porque Directory.Build.props
                                                  # usa DebugType=embedded en Release.
)

$apps = @(
  @{ Proyecto = 'CamaraPortuaria.UI'; Salida = 'CamaraPortuaria' },
  @{ Proyecto = 'CentroMaritimo.UI';  Salida = 'CentroMaritimo'  }
)

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }

foreach ($app in $apps) {
  $out = Join-Path $dist $app.Salida
  Write-Host "Publicando $($app.Proyecto) -> $out" -ForegroundColor Cyan
  dotnet publish $app.Proyecto @common -o $out
  if ($LASTEXITCODE -ne 0) { throw "Falló el publish de $($app.Proyecto)" }
}

Write-Host "`nListo. Ejecutables generados:" -ForegroundColor Green
Get-ChildItem -Path $dist -Recurse -Filter *.exe | ForEach-Object {
  '  {0}  ({1:N1} MB)' -f $_.FullName, ($_.Length / 1MB)
}
