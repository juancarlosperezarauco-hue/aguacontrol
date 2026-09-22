param([string]$Report)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$workspace=Split-Path $projectRoot -Parent
if (!$Report) { $Report=Join-Path $workspace 'output/analisis/Validacion_SIG.json' }
$reportPath=[System.IO.Path]::GetFullPath($Report)
New-Item -ItemType Directory -Force -Path (Split-Path $reportPath -Parent) | Out-Null
$sdk=Join-Path $workspace '.tools/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $sdk)) { $sdk=(Get-Command dotnet).Source }
$env:AQUA_SIG_REPORT=$reportPath
$env:ASPNETCORE_ENVIRONMENT='Development'
if (!$env:ConnectionStrings__Aqua) { $env:ConnectionStrings__Aqua='Server=np:\\.\pipe\sql\query;Database=AquaControlDev;Integrated Security=true;Encrypt=false;TrustServerCertificate=true' }
& $sdk run --no-build --project (Join-Path $projectRoot 'src/AquaControl.API') -- --validate-sig
if ($LASTEXITCODE -ne 0) { throw 'Validación SIG no completada.' }
