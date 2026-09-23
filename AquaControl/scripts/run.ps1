param(
    [int]$Port=5080,
    [Alias('Host')]
    [string]$ListenHost='localhost',
    [switch]$SandboxPayments
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$workspace=Split-Path $projectRoot -Parent
$sdk=Join-Path $workspace '.tools/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $sdk)) { $sdk=(Get-Command dotnet).Source }
$env:ASPNETCORE_ENVIRONMENT='Development'
if (!$env:ConnectionStrings__Aqua) { $env:ConnectionStrings__Aqua='Server=np:\\.\pipe\sql\query;Database=AquaControlDev;Integrated Security=true;Encrypt=false;TrustServerCertificate=true' }
$listenHost = if ($ListenHost -eq 'lan') { '0.0.0.0' } else { $ListenHost }
if ($listenHost -ne 'localhost' -and $listenHost -ne '127.0.0.1') { $env:AllowedHosts='*' }
$env:Payments__Sandbox=$SandboxPayments.IsPresent.ToString()
& $sdk run --no-build --project (Join-Path $projectRoot 'src/AquaControl.API') --urls "http://${listenHost}:$Port"
