param([string]$Folder)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$workspace=Split-Path $projectRoot -Parent
if (!$Folder) { $Folder=Join-Path $workspace 'DatosSIG_Reproj' }
$sdk=Join-Path $workspace '.tools/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $sdk)) { $sdk=(Get-Command dotnet).Source }
$env:AQUA_SIG_PATH=(Resolve-Path -LiteralPath $Folder).Path
$env:ASPNETCORE_ENVIRONMENT='Development'
if (!$env:ConnectionStrings__Aqua) { $env:ConnectionStrings__Aqua='Server=np:\\.\pipe\sql\query;Database=AquaControlDev;Integrated Security=true;Encrypt=false;TrustServerCertificate=true' }
& $sdk run --no-build --project (Join-Path $projectRoot 'src/AquaControl.API') -- --import-sig
if ($LASTEXITCODE -ne 0) { throw 'Importación no completada.' }
