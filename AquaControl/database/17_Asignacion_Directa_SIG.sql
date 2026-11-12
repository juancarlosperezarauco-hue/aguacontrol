/* Problema: la asignación directa puede repetirse ante un reintento de red.
   Solución: identificador único y huella del pedido en la ruta.
   Incremento aditivo e idempotente. No modifica rutas, órdenes ni clientes anteriores.
   Rollback de aplicación: conservar columnas e índice; no eliminar historial generado.
*/
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.RutasCampo','U') IS NULL THROW 51017,'Ejecute primero el incremento 16.',1;
IF COL_LENGTH('dbo.RutasCampo','RequestId') IS NULL ALTER TABLE dbo.RutasCampo ADD RequestId uniqueidentifier NULL;
IF COL_LENGTH('dbo.RutasCampo','SourceHash') IS NULL ALTER TABLE dbo.RutasCampo ADD SourceHash nvarchar(64) NOT NULL CONSTRAINT DF_RutasCampo_SourceHash DEFAULT N'';
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.RutasCampo') AND name='IX_RutasCampo_RequestId')
 EXEC(N'CREATE UNIQUE INDEX IX_RutasCampo_RequestId ON dbo.RutasCampo(RequestId) WHERE RequestId IS NOT NULL');
COMMIT;
