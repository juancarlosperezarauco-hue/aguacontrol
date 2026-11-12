param([string]$SqlServer='localhost',[string]$Database='AquaControlDev')
$ErrorActionPreference='Stop'
if($Database -notmatch '^AquaControl[A-Za-z0-9_]*$'){throw 'Seleccione una base AquaControl válida.'}
$projectRoot=Split-Path $PSScriptRoot -Parent
$sqlcmd=(Get-Command sqlcmd -ErrorAction Stop).Source
$env:ConnectionStrings__Aqua="Server=$SqlServer;Database=$Database;Integrated Security=true;Encrypt=false;TrustServerCertificate=true"
$env:ASPNETCORE_ENVIRONMENT='Development'
& $sqlcmd -S $SqlServer -E -d $Database -b -i (Join-Path $projectRoot 'database/15_Campo_Movil_Ubicacion_Evidencias.sql')
if($LASTEXITCODE -ne 0){throw 'Falló el incremento SQL móvil.'}
& dotnet build (Join-Path $projectRoot 'src/AquaControl.API')
if($LASTEXITCODE -ne 0){throw 'Detenga AquaControl y corrija la compilación antes de continuar.'}
& dotnet run --no-build --project (Join-Path $projectRoot 'src/AquaControl.API') -- --upgrade-roles
if($LASTEXITCODE -ne 0){throw 'Falló la actualización de permisos de campo.'}
Write-Host 'Módulo móvil actualizado. Reinicie el servidor y vuelva a iniciar sesión.'
