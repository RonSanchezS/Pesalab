[CmdletBinding()]
param (
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "Iniciando compilacion y pruebas PesajeFaeneo" -ForegroundColor Cyan
Write-Host "Configuracion: $Configuration" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# 1. Restore
Write-Host "`n[1/3] Restaurando dependencias..." -ForegroundColor Yellow
dotnet restore src/PesajeFaeneo.sln
if ($LASTEXITCODE -ne 0) {
    Write-Error "Fallo la restauracion de dependencias."
    exit 1
}

# 2. Build
Write-Host "`n[2/3] Compilando solucion ($Configuration)..." -ForegroundColor Yellow
dotnet build src/PesajeFaeneo.sln -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "Fallo la compilacion."
    exit 1
}

# 3. Test con Cobertura
Write-Host "`n[3/3] Ejecutando pruebas unitarias con cobertura..." -ForegroundColor Yellow
$testResultsDir = Join-Path $PSScriptRoot "TestResults"
if (Test-Path $testResultsDir) {
    Remove-Item -Recurse -Force $testResultsDir
}

dotnet test src/PesajeFaeneo.sln -c $Configuration --no-build `
    --collect:"XPlat Code Coverage" `
    --results-directory $testResultsDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Fallaron las pruebas unitarias."
    exit 1
}

# Normalizar archivo de cobertura
$coverageFile = Get-ChildItem -Path $testResultsDir -Filter "coverage.cobertura.xml" -Recurse | Select-Object -First 1
if ($coverageFile) {
    $targetPath = Join-Path $PSScriptRoot "coverage.cobertura.xml"
    Copy-Item -Path $coverageFile.FullName -Destination $targetPath -Force
    Write-Host "Archivo de cobertura generado: $targetPath" -ForegroundColor Green
}

Write-Host "`n=============================================" -ForegroundColor Green
Write-Host "Compilacion y pruebas completadas con exito." -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green

exit 0
