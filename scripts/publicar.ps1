# Publica PesajeFaeneo como aplicacion autocontenida win-x64 y la verifica con --autoprueba (F9-01).
# ASCII puro: Windows PowerShell 5.1 lee los .ps1 sin BOM como ANSI (regla 0.19 del plan F9).
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publicar.ps1 [-SinAutoprueba]
[CmdletBinding()]
param(
    [switch]$SinAutoprueba
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $raiz

function Fallar([string]$mensaje) {
    Write-Host "ERROR: $mensaje" -ForegroundColor Red
    exit 1
}

# 1. Version: unica fuente en Directory.Build.props (D1)
[xml]$props = Get-Content -Raw -LiteralPath (Join-Path $raiz 'Directory.Build.props')
$version = @($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
if (-not ($version -match '^\d+\.\d+\.\d+$')) { Fallar "Version invalida en Directory.Build.props: '$version'." }
Write-Host "Version: $version"

# 2. Carpeta de salida, siempre dentro de artifacts (se borra entera antes de publicar)
$artifacts = Join-Path $raiz 'artifacts'
$salida = Join-Path $artifacts 'publish\win-x64'
if (-not $salida.StartsWith($artifacts + '\', [StringComparison]::OrdinalIgnoreCase)) { Fallar 'La carpeta de salida no esta dentro de artifacts.' }
if (Test-Path -LiteralPath $salida) { Remove-Item -LiteralPath $salida -Recurse -Force }

# 3. Publicar. Sin --no-restore: la restauracion con -r win-x64 baja los paquetes de runtime (necesita red, H1).
#    RuntimeIdentifier/SelfContained solo aqui, nunca en un .csproj (H2, D3).
$proyecto = Join-Path $raiz 'src\PesajeFaeneo.WinForms\PesajeFaeneo.WinForms.csproj'
& dotnet publish $proyecto -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:PublishReadyToRun=false -p:SatelliteResourceLanguages=es -o $salida
if ($LASTEXITCODE -ne 0) { Fallar "dotnet publish termino con codigo $LASTEXITCODE." }

# 4. Archivos obligatorios (H3, H4, H5) y prohibidos
$obligatorios = @(
    'PesajeFaeneo.WinForms.exe', 'PesajeFaeneo.WinForms.dll', 'PesajeFaeneo.Application.dll',
    'PesajeFaeneo.Infrastructure.dll', 'PesajeFaeneo.Domain.dll', 'appsettings.json',
    'coreclr.dll', 'hostfxr.dll', 'System.Windows.Forms.dll', 'Microsoft.Data.Sqlite.dll',
    'e_sqlite3.dll', 'QuestPdfSkia.dll', 'System.Data.OleDb.dll'
)
foreach ($a in $obligatorios) {
    if (-not (Test-Path -LiteralPath (Join-Path $salida $a))) { Fallar "Falta $a en la publicacion." }
}

# QuestPDF empaqueta LatoFont bajo runtimes/any/native; dotnet publish con RID win-x64 omite any/native.
$lato = Get-ChildItem -LiteralPath $salida -Recurse -Filter 'Lato-Regular.ttf' | Select-Object -First 1
if (-not $lato) {
    $candidatosLato = @(
        (Join-Path $env:USERPROFILE '.nuget\packages\questpdf\2024.3.0\runtimes\any\native\LatoFont'),
        (Join-Path $raiz 'src\PesajeFaeneo.WinForms\bin\Release\net8.0-windows\runtimes\any\native\LatoFont'),
        (Join-Path $raiz 'src\PesajeFaeneo.WinForms\bin\Debug\net8.0-windows\runtimes\any\native\LatoFont')
    )
    $origenLato = $candidatosLato | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
    if ($origenLato) {
        $destinoLato = Join-Path $salida 'runtimes\any\native\LatoFont'
        if (-not (Test-Path -LiteralPath $destinoLato)) {
            New-Item -ItemType Directory -Force -Path (Join-Path $salida 'runtimes\any\native') | Out-Null
            Copy-Item -LiteralPath $origenLato -Destination $destinoLato -Recurse -Force
        }
        $lato = Get-ChildItem -LiteralPath $salida -Recurse -Filter 'Lato-Regular.ttf' | Select-Object -First 1
    }
}
if (-not $lato) { Fallar 'Falta la fuente Lato-Regular.ttf que usa QuestPDF.' }

$prohibidos = @(Get-ChildItem -LiteralPath $salida -Recurse -File | Where-Object {
    $_.Name -like '*Tests*' -or $_.Name -like 'xunit*' -or $_.Name -like '*.db' -or $_.Name -like '*.db-wal' -or $_.Name -like '*.db-shm'
})
if ($prohibidos.Count -gt 0) { Fallar ('Archivos que no deben publicarse: ' + (($prohibidos | ForEach-Object { $_.Name }) -join ', ')) }

# 5. La version del ejecutable coincide con Directory.Build.props
$exe = Join-Path $salida 'PesajeFaeneo.WinForms.exe'
$productVersion = (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion
if (-not $productVersion.StartsWith($version)) { Fallar "ProductVersion '$productVersion' no coincide con $version." }

# 6. Autoprueba (D5) en una carpeta temporal nueva; nunca en ProgramData
if (-not $SinAutoprueba) {
    $carpeta = Join-Path ([IO.Path]::GetTempPath()) ('PesajeFaeneo-autoprueba-' + [guid]::NewGuid().ToString('N'))
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $exe
    $psi.Arguments = '--autoprueba "' + $carpeta.TrimEnd('\') + '"'
    $psi.UseShellExecute = $false
    $proc = [System.Diagnostics.Process]::Start($psi)
    if (-not $proc.WaitForExit(300000)) { $proc.Kill(); Fallar 'La autoprueba no termino en 5 minutos.' }
    $codigo = $proc.ExitCode
    $json = Join-Path $carpeta 'autoprueba.json'
    if (-not (Test-Path -LiteralPath $json)) { Fallar "La autoprueba no escribio su resultado (codigo $codigo)." }
    $r = Get-Content -Raw -LiteralPath $json | ConvertFrom-Json
    Write-Host ("Autoprueba v{0} ({1}, 64 bits: {2})" -f $r.Version, $r.Runtime, $r.Proceso64Bits)
    foreach ($paso in $r.Pasos) {
        $estado = 'FALLA'
        if ($paso.Exito) { $estado = 'OK' }
        Write-Host ('  {0,-12} {1,-6} {2,6} ms  {3}' -f $paso.Nombre, $estado, $paso.Milisegundos, $paso.Mensaje)
    }
    Remove-Item -LiteralPath $carpeta -Recurse -Force -ErrorAction SilentlyContinue
    if ($codigo -ne 0) { Fallar "La autoprueba fallo (codigo $codigo)." }
}

# 7. Resumen
$archivos = @(Get-ChildItem -LiteralPath $salida -Recurse -File)
$mb = [math]::Round((($archivos | Measure-Object -Property Length -Sum).Sum / 1MB), 1)
$resumen = "Version=$version`r`nArchivos=$($archivos.Count)`r`nTamanoMB=$mb`r`n"
[IO.File]::WriteAllText((Join-Path $artifacts 'publish\publicacion-win-x64.txt'), $resumen, [Text.Encoding]::ASCII)
Write-Host "Publicacion OK: $salida ($($archivos.Count) archivos, $mb MB)" -ForegroundColor Green
exit 0
