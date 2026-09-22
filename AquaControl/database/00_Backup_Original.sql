-- Respaldo COPY_ONLY; no modifica tablas. No sobrescribe respaldos anteriores.
USE master;
DECLARE @file nvarchar(4000)=CONVERT(nvarchar(3000),SERVERPROPERTY('InstanceDefaultBackupPath'))+N'\VisorDatosSIG_AquaControl_'+REPLACE(REPLACE(CONVERT(nvarchar(30),SYSDATETIME(),126),':',''),'.','')+N'.bak';
BACKUP DATABASE VisorDatosSIG TO DISK=@file WITH COPY_ONLY,CHECKSUM;
RESTORE VERIFYONLY FROM DISK=@file WITH CHECKSUM;
SELECT @file AS RespaldoOriginal;
