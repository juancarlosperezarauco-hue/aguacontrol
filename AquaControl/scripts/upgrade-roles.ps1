param([string]$ConnectionString)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if($ConnectionString){$env:ConnectionStrings__Aqua=$ConnectionString}
if(!$env:ConnectionStrings__Aqua -or $env:ConnectionStrings__Aqua -notmatch '(?i)(Database|Initial Catalog)\s*=\s*AquaControl'){
 throw 'Defina ConnectionStrings__Aqua apuntando a una base AquaControl. No se admite la base original VisorDatosSIG.'
}
$env:ASPNETCORE_ENVIRONMENT='Development'
$sdk=(Get-Command dotnet -ErrorAction Stop).Source
& $sdk build (Join-Path $projectRoot 'src/AquaControl.API/AquaControl.API.csproj')
if($LASTEXITCODE -ne 0){throw 'No se pudo compilar. Detenga la instancia local de AquaControl antes de actualizar.'}
& $sdk run --no-build --project (Join-Path $projectRoot 'src/AquaControl.API') -- --upgrade-roles
if($LASTEXITCODE -ne 0){throw 'Actualización fallida. La transacción revierte los cambios.'}
Write-Host 'Roles actualizados. Reinicie AquaControl; todos deben ingresar nuevamente.'
