param([string]$SqlServer='localhost',[string]$Database='AquaControlDev')
$ErrorActionPreference='Stop'
if($Database -notmatch '^AquaControl[A-Za-z0-9_]*$'){throw 'Seleccione una base AquaControl válida; nunca la BD SIG original.'}
$projectRoot=Split-Path $PSScriptRoot -Parent
$sqlcmd=(Get-Command sqlcmd -ErrorAction Stop).Source
$backupName=$Database+'_AntesAlcance_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.bak'
# SQL Server guarda el respaldo en su carpeta predeterminada, con permisos de su servicio.
$backupSql="DECLARE @path nvarchar(4000)=CONVERT(nvarchar(3000),SERVERPROPERTY('InstanceDefaultBackupPath'))+N'$backupName'; BACKUP DATABASE [$Database] TO DISK=@path WITH COPY_ONLY,CHECKSUM; RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM; SELECT @path AS RespaldoVerificado;"
& $sqlcmd -S $SqlServer -E -d master -b -Q $backupSql
if($LASTEXITCODE -ne 0){throw 'No se verificó el respaldo. No se aplicó la actualización.'}
& $sqlcmd -S $SqlServer -E -d $Database -b -i (Join-Path $projectRoot 'database/15_Campo_Movil_Ubicacion_Evidencias.sql')
if($LASTEXITCODE -ne 0){throw 'Falló el incremento 15.'}
& $sqlcmd -S $SqlServer -E -d $Database -b -i (Join-Path $projectRoot 'database/16_Rutas_Visitas_Recorridos.sql')
if($LASTEXITCODE -ne 0){throw 'Falló el incremento 16.'}
& $sqlcmd -S $SqlServer -E -d $Database -b -i (Join-Path $projectRoot 'database/17_Asignacion_Directa_SIG.sql')
if($LASTEXITCODE -ne 0){throw 'Falló el incremento 17.'}
& dotnet build (Join-Path $projectRoot 'src/AquaControl.API')
if($LASTEXITCODE -ne 0){throw 'Detenga la instancia anterior de AquaControl y vuelva a compilar.'}
Write-Host 'Alcance actualizado. Ejecute scripts/run-mobile.ps1 e instale la APK 0.3 en los teléfonos.'
