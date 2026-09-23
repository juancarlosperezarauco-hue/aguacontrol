param(
    [string]$ConnectionString
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$workspace = Split-Path $projectRoot -Parent
$sdk = Join-Path $workspace '.tools/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $sdk)) { $sdk = (Get-Command dotnet -ErrorAction Stop).Source }
if ($ConnectionString) { $env:ConnectionStrings__Aqua = $ConnectionString }
if (!$env:ConnectionStrings__Aqua) {
    throw 'Defina -ConnectionString o ConnectionStrings__Aqua. Ejemplo: Server=localhost\SQLEXPRESS;Database=AquaControlDev;Integrated Security=true;Encrypt=false;TrustServerCertificate=true'
}

try {
    & $sdk run --project (Join-Path $projectRoot 'src/AquaControl.API') -- --verify
    if ($LASTEXITCODE -ne 0) { throw 'La verificación devolvió un error.' }
    Write-Host 'Diagnóstico correcto: puede ejecutar scripts/run.ps1.' -ForegroundColor Green
}
catch {
    Write-Host "Diagnóstico falló: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host 'Si la base no existe, defina AQUA_BOOTSTRAP_PASSWORD y ejecute scripts/setup.ps1 -Initialize.' -ForegroundColor Yellow
    throw
}
