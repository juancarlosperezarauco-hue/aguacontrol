param([string]$SqlServer='localhost',[string]$Database='AquaControlDev')
$ErrorActionPreference='Stop'
if($Database -notmatch '^AquaControl[A-Za-z0-9_]*$'){throw 'Seleccione una base AquaControl válida.'}
$sqlcmd=(Get-Command sqlcmd -ErrorAction Stop).Source
$name=$Database+'_Equipo_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.bak'
$query="DECLARE @path nvarchar(4000)=CONVERT(nvarchar(3000),SERVERPROPERTY('InstanceDefaultBackupPath'))+N'$name'; BACKUP DATABASE [$Database] TO DISK=@path WITH COPY_ONLY,CHECKSUM; RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM; SELECT @path AS RespaldoVerificado;"
& $sqlcmd -S $SqlServer -E -d master -b -Q $query
if($LASTEXITCODE -ne 0){throw 'El respaldo no quedó verificado.'}
Write-Host 'Conserve el .bak en privado. Las fotos se respaldan aparte: copie la carpeta indicada por Storage__EvidencePath o src/AquaControl.API/App_Data/evidence.'
