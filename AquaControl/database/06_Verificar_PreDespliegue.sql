/* AquaControl — verificación de solo lectura antes de desplegar.
   Ejecutar en la base de destino después de aplicar las actualizaciones necesarias. */
SET NOCOUNT ON;

DECLARE @faltantes TABLE (Elemento nvarchar(160) NOT NULL);

IF OBJECT_ID('dbo.ImportacionesSIG','U') IS NULL INSERT @faltantes VALUES (N'Tabla ImportacionesSIG');
IF OBJECT_ID('dbo.IncidenciasSIG','U') IS NULL INSERT @faltantes VALUES (N'Tabla IncidenciasSIG');
IF COL_LENGTH('dbo.ImportacionesSIG','SourceFile') IS NULL INSERT @faltantes VALUES (N'ImportacionesSIG.SourceFile');
IF COL_LENGTH('dbo.ImportacionesSIG','Mode') IS NULL INSERT @faltantes VALUES (N'ImportacionesSIG.Mode');
IF COL_LENGTH('dbo.ImportacionesSIG','Status') IS NULL INSERT @faltantes VALUES (N'ImportacionesSIG.Status');
IF COL_LENGTH('dbo.ImportacionesSIG','StartedAt') IS NULL INSERT @faltantes VALUES (N'ImportacionesSIG.StartedAt');
IF COL_LENGTH('dbo.ImportacionesSIG','CompletedAt') IS NULL INSERT @faltantes VALUES (N'ImportacionesSIG.CompletedAt');
IF COL_LENGTH('dbo.ImportacionesSIG','DurationMilliseconds') IS NULL INSERT @faltantes VALUES (N'ImportacionesSIG.DurationMilliseconds');
IF COL_LENGTH('dbo.IncidenciasSIG','Severity') IS NULL INSERT @faltantes VALUES (N'IncidenciasSIG.Severity');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Conexiones') AND name IN ('IX_Conexiones_FixedCodeId', 'UX_Conexiones_CodigoFijo')) INSERT @faltantes VALUES (N'Índice de Código Fijo en Conexiones');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.ImportacionesSIG') AND name='IX_ImportacionesSIG_UserId') INSERT @faltantes VALUES (N'Índice IX_ImportacionesSIG_UserId');

SELECT DB_NAME() AS BaseVerificada,
       (SELECT COUNT(*) FROM dbo.Usuarios WHERE Activo=1) AS UsuariosActivos,
       (SELECT COUNT(*) FROM dbo.Roles WHERE Active=1) AS RolesActivos,
       (SELECT COUNT(*) FROM dbo.CodigosFijos) AS CodigosFijos,
       (SELECT COUNT(*) FROM dbo.Lotes) AS Lotes,
       (SELECT COUNT(*) FROM dbo.Manzanas) AS Manzanas,
       (SELECT COUNT(*) FROM dbo.Vias) AS Vias;

SELECT Elemento AS ElementoFaltante FROM @faltantes;

IF EXISTS (SELECT 1 FROM @faltantes)
    THROW 51001, 'El esquema no está actualizado. Revise database/05_Completar_Bitacora_Migrador_SIG.sql y los scripts previos.', 1;

PRINT 'Verificación de predespliegue correcta.';
