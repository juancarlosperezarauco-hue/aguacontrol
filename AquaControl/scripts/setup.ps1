param([switch]$Initialize)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$workspace = Split-Path $projectRoot -Parent
$sdk = Join-Path $workspace '.tools/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $sdk)) {
    $systemSdk = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($systemSdk) { $sdk = $systemSdk.Source } else { throw 'Instale .NET SDK 10 o ejecute el instalador oficial dotnet-install en .tools/dotnet.' }
}
$vendor = Join-Path $projectRoot 'Web/vendor'
New-Item -ItemType Directory -Force $vendor | Out-Null
$assets = @{
 'qrcode.js'='https://cdn.jsdelivr.net/npm/qrcode-generator@1.4.4/qrcode.js'
 'leaflet.js'='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'
 'leaflet.css'='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'
 'bootstrap-grid.min.css'='https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap-grid.min.css'
}
foreach ($asset in $assets.GetEnumerator()) {
    $target = Join-Path $vendor $asset.Key
    if (!(Test-Path -LiteralPath $target)) { Invoke-WebRequest -Uri $asset.Value -OutFile $target }
}
& $sdk build (Join-Path $projectRoot 'src/AquaControl.API/AquaControl.API.csproj')
if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar.' }
if ($Initialize) {
    if (!$env:AQUA_BOOTSTRAP_PASSWORD) { throw 'Defina AQUA_BOOTSTRAP_PASSWORD (12 caracteres como mínimo, letras y números).' }
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    & $sdk run --no-build --project (Join-Path $projectRoot 'src/AquaControl.API') -- --init
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo inicializar.' }
}
