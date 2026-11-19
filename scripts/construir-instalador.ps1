# Construye el instalador Inno Setup de PesajeFaeneo (F9-03).
# ASCII puro: Windows PowerShell 5.1 lee los .ps1 sin BOM como ANSI (regla 0.19 del plan F9).
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File scripts\construir-instalador.ps1 [-SinPublicar]
# Este script NO ejecuta el instalador (regla 0.21).
[CmdletBinding()]
param(
    [switch]$SinPublicar
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $raiz

function Fallar([string]$mensaje) {
    Write-Host "ERROR: $mensaje" -ForegroundColor Red
    exit 1
}

# 1. Inno Setup 6.3 o superior (H7)
$candidatos = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
)
$iscc = $candidatos | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
if (-not $iscc) {
    $comando = Get-Command 'iscc' -ErrorAction SilentlyContinue
    if ($comando) { $iscc = $comando.Source }
}
if (-not $iscc) { Fallar 'No se encontro Inno Setup (ISCC.exe). Instalelo con: winget install --id JRSoftware.InnoSetup -e' }
function LeerVersion([string]$texto) {
    $limpio = ([string]$texto).Trim() -replace '[^0-9\.].*$', ''
    $limpio = $limpio.Trim('.')
    if (-not ($limpio -match '^\d+(\.\d+){1,3}$')) { return [version]'0.0' }
    return [version]$limpio
}
$textoVersion = (Get-Item -LiteralPath $iscc).VersionInfo.ProductVersion
$versionIscc = LeerVersion $textoVersion
# Inno Setup 6.7+ publica sus .exe sin numero de version (0.0.0.0): se toma del registro de desinstalacion.
if ($versionIscc -lt [version]'1.0') {
    $claves = @(
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
        'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
        'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1'
    )
    foreach ($clave in $claves) {
        $registro = Get-ItemProperty -LiteralPath $clave -ErrorAction SilentlyContinue
        if ($registro -and $registro.DisplayVersion) {
            $textoVersion = ([string]$registro.DisplayVersion).Trim()
            $versionIscc = LeerVersion $textoVersion
            break
        }
    }
}
# Si aun asi no se conoce, decide el propio .iss (#error si el preprocesador es anterior a 6.3).
if (($versionIscc -ge [version]'1.0') -and ($versionIscc -lt [version]'6.3')) { Fallar "Se necesita Inno Setup 6.3 o superior (encontrado $textoVersion)." }
Write-Host "Inno Setup: $iscc ($textoVersion)"

# 2. Publicacion (scripts\publicar.ps1), salvo -SinPublicar
if (-not $SinPublicar) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'publicar.ps1')
    if ($LASTEXITCODE -ne 0) { Fallar 'La publicacion fallo; no se construye el instalador.' }
}
$origen = (Join-Path $raiz 'artifacts\publish\win-x64').TrimEnd('\')
if (-not (Test-Path -LiteralPath (Join-Path $origen 'PesajeFaeneo.WinForms.exe'))) { Fallar 'No hay publicacion en artifacts\publish\win-x64. Ejecute scripts\publicar.ps1.' }

# 3. Version (unica fuente: Directory.Build.props)
[xml]$props = Get-Content -Raw -LiteralPath (Join-Path $raiz 'Directory.Build.props')
$version = @($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
if (-not ($version -match '^\d+\.\d+\.\d+$')) { Fallar "Version invalida en Directory.Build.props: '$version'." }

# 4. Compilar. Rutas sin barra final: "C:\x\" escaparia la comilla de cierre (H12).
$salida = (Join-Path $raiz 'artifacts\instalador').TrimEnd('\')
New-Item -ItemType Directory -Force -Path $salida | Out-Null
$iss = Join-Path $raiz 'instalador\PesajeFaeneo.iss'
& $iscc "/DVersionApp=$version" "/DOrigen=$origen" "/DSalida=$salida" $iss
if ($LASTEXITCODE -ne 0) { Fallar "ISCC termino con codigo $LASTEXITCODE." }

# 5. Resultado y su SHA-256
$nombre = "Capataz-Setup-$version-x64.exe"
$setup = Join-Path $salida $nombre
if (-not (Test-Path -LiteralPath $setup)) { Fallar "No se genero $nombre." }
$hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($setup + '.sha256', "$hash  $nombre`r`n", [Text.Encoding]::ASCII)
$mb = [math]::Round(((Get-Item -LiteralPath $setup).Length / 1MB), 1)
Write-Host "Instalador OK: $setup ($mb MB)" -ForegroundColor Green
Write-Host "SHA256: $hash"
exit 0
