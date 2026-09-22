param([int]$Port=5080,[switch]$SandboxPayments)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$workspace=Split-Path $projectRoot -Parent
$sdk=Join-Path $workspace '.tools/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $sdk)) { $sdk=(Get-Command dotnet).Source }
$env:ASPNETCORE_ENVIRONMENT='Development'
if (!$env:ConnectionStrings__Aqua) { $env:ConnectionStrings__Aqua='Server=np:\\.\pipe\sql\query;Database=AquaControlDev;Integrated Security=true;Encrypt=false;TrustServerCertificate=true' }
$env:Payments__Sandbox=$SandboxPayments.IsPresent.ToString()
& $sdk run --no-build --project (Join-Path $projectRoot 'src/AquaControl.API') --urls "http://localhost:$Port"
