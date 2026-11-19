# Genera las capturas de pantalla reales para los manuales de usuario (build de capturas).
# ASCII puro: Windows PowerShell 5.1 lee los .ps1 sin BOM como ANSI.
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File scripts\capturas-manual.ps1 [-Salida <carpeta>] [-Solo op-,su-] [-Reusar] [-SinCompilar] [-SoloVacio]
#
# Que hace:
#  1. Compila el proyecto WinForms con la constante CAPTURAS_MANUAL (-p:CapturasManual=true). bin y obj van a una carpeta
#     aparte (-p:CapturasRaiz), asi que NO se mezclan con los de un build normal. El ejecutable publicado y el instalador
#     (scripts\publicar.ps1) no definen la constante y quedan exactamente igual.
#  2. Ejecuta ese build con una carpeta de datos de demostracion (--datos): NUNCA usa C:\ProgramData\PesajeFaeneo.
#     La carpeta se borra y se vuelve a sembrar en cada corrida (salvo -Reusar).
#  3. El guion (--capturas) abre las pantallas reales, las captura con PrintWindow y escribe un PNG por paso mas
#     capturas.json en la carpeta de salida. Las claves de prueba estan en capturas-manual.config.json.
# Las ventanas aparecen un momento en pantalla: no use el mouse ni el teclado mientras corre.
[CmdletBinding()]
param(
    [string]$Salida,
    [string]$RaizBuild,
    [string]$Solo = '',
    [switch]$Reusar,
    [switch]$SinCompilar,
    [switch]$SoloVacio
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $raiz

function Fallar([string]$mensaje) {
    Write-Host "ERROR: $mensaje" -ForegroundColor Red
    exit 1
}

if (-not $RaizBuild) { $RaizBuild = Join-Path $raiz 'artifacts\capturas-build' }
if (-not $Salida) { $Salida = Join-Path $raiz 'artifacts\capturas-manual' }
$RaizBuild = [IO.Path]::GetFullPath($RaizBuild)
$Salida = [IO.Path]::GetFullPath($Salida)
$datos = Join-Path $RaizBuild 'datos'
$datosVacio = Join-Path $RaizBuild 'datos-vacio'
$config = Join-Path $PSScriptRoot 'capturas-manual.config.json'
if (-not (Test-Path -LiteralPath $config)) { Fallar "Falta $config." }

# Seguridad: las carpetas de trabajo nunca pueden ser los datos reales.
$real = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'PesajeFaeneo'))
foreach ($c in @($datos, $datosVacio, $Salida, $RaizBuild)) {
    if ($c.TrimEnd('\').Equals($real.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase) -or $c.StartsWith($real + '\', [StringComparison]::OrdinalIgnoreCase)) {
        Fallar "La ruta '$c' esta dentro de los datos reales del sistema ($real)."
    }
}

# 1. Compilar
$proyecto = Join-Path $raiz 'src\PesajeFaeneo.WinForms\PesajeFaeneo.WinForms.csproj'
$exe = Join-Path $RaizBuild 'bin\PesajeFaeneo.WinForms\Release\net8.0-windows\PesajeFaeneo.WinForms.exe'
if (-not $SinCompilar) {
    $rb = $RaizBuild.Replace('\', '/')
    & dotnet build $proyecto -c Release -p:CapturasManual=true "-p:CapturasRaiz=$rb"
    if ($LASTEXITCODE -ne 0) { Fallar "dotnet build termino con codigo $LASTEXITCODE." }
}
if (-not (Test-Path -LiteralPath $exe)) { Fallar "No se encontro el ejecutable de capturas: $exe" }

# 2. Salida limpia. Con -Solo o -SoloVacio se conservan las capturas anteriores (se reemplazan solo las que se vuelven a tomar).
if (-not $Solo -and -not $SoloVacio -and (Test-Path -LiteralPath $Salida)) { Remove-Item -LiteralPath $Salida -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Salida | Out-Null

# 3. Ejecutar el guion. Corrida 1: datos de demostracion completos. Corrida 2 (solo sin -Solo): datos SIN pesajes, para las
#    pantallas anteriores a la primera boleta (Parametros con el numero editable).
function EjecutarGuion([string]$carpetaDatos, [string[]]$extra) {
    if (-not $Reusar -and (Test-Path -LiteralPath $carpetaDatos)) { Remove-Item -LiteralPath $carpetaDatos -Recurse -Force }
    $argumentos = @('--datos', "`"$carpetaDatos`"", '--capturas', "`"$Salida`"", '--config', "`"$config`"") + $extra
    Write-Host "Ejecutando el guion de capturas (no use el mouse ni el teclado)..."
    $proceso = Start-Process -FilePath $exe -ArgumentList $argumentos -PassThru -Wait
    return $proceso.ExitCode
}

$codigo = 0
if (-not $SoloVacio) {
    $extra = @()
    if ($Solo) { $extra += @('--solo', $Solo) }
    $codigo = EjecutarGuion $datos $extra
}
if ((-not $Solo) -or $SoloVacio) {
    $c2 = EjecutarGuion $datosVacio @('--sin-pesajes')
    if ($c2 -ne 0) { $codigo = $c2 }
}

$log = Join-Path $Salida 'capturas.log'
if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Encoding UTF8 | Select-Object -Last 40 }
$png = @(Get-ChildItem -LiteralPath $Salida -Filter '*.png' -ErrorAction SilentlyContinue).Count
Write-Host "Codigo de salida: $codigo. Capturas (PNG): $png. Carpeta: $Salida"
exit $codigo