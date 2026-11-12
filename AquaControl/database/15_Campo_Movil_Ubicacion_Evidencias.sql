SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF OBJECT_ID('dbo.JornadasCampo') IS NULL BEGIN
 CREATE TABLE dbo.JornadasCampo(Id int IDENTITY PRIMARY KEY,UserId int NOT NULL,StartedAt datetime2 NOT NULL,EndedAt datetime2 NULL,Latitude float NULL,Longitude float NULL,Accuracy float NULL,CapturedAt datetime2 NULL,ReceivedAt datetime2 NULL,Version rowversion NOT NULL,
 CONSTRAINT FK_JornadasCampo_Usuarios FOREIGN KEY(UserId) REFERENCES dbo.Usuarios(IdUsuario),
 CONSTRAINT CK_JornadasCampo_Posicion CHECK((Latitude IS NULL AND Longitude IS NULL) OR (Latitude BETWEEN -90 AND 90 AND Longitude BETWEEN -180 AND 180)),
 CONSTRAINT CK_JornadasCampo_Precision CHECK(Accuracy IS NULL OR Accuracy BETWEEN 0 AND 1000));
 CREATE UNIQUE INDEX IX_JornadasCampo_UserId ON dbo.JornadasCampo(UserId) WHERE EndedAt IS NULL;
END
GO
IF COL_LENGTH('dbo.Evidencias','ConnectionId') IS NULL ALTER TABLE dbo.Evidencias ADD ConnectionId int NULL;
IF COL_LENGTH('dbo.Evidencias','MeterId') IS NULL ALTER TABLE dbo.Evidencias ADD MeterId int NULL;
IF COL_LENGTH('dbo.Evidencias','MeterSerial') IS NULL ALTER TABLE dbo.Evidencias ADD MeterSerial nvarchar(80) NOT NULL CONSTRAINT DF_Evidencias_MeterSerial DEFAULT N'';
GO
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_Evidencias_Conexiones_ConnectionId') ALTER TABLE dbo.Evidencias ADD CONSTRAINT FK_Evidencias_Conexiones_ConnectionId FOREIGN KEY(ConnectionId) REFERENCES dbo.Conexiones(Id);
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_Evidencias_Medidores_MeterId') ALTER TABLE dbo.Evidencias ADD CONSTRAINT FK_Evidencias_Medidores_MeterId FOREIGN KEY(MeterId) REFERENCES dbo.Medidores(Id);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Evidencias_ConnectionId' AND object_id=OBJECT_ID('dbo.Evidencias')) CREATE INDEX IX_Evidencias_ConnectionId ON dbo.Evidencias(ConnectionId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Evidencias_MeterId' AND object_id=OBJECT_ID('dbo.Evidencias')) CREATE INDEX IX_Evidencias_MeterId ON dbo.Evidencias(MeterId);
GO
-- No se reetiquetan fotos antiguas con el medidor actual: su identidad histórica no se puede inferir.
-- Después de este incremento, ejecutar --upgrade-roles para incorporar field.share/field.monitor.
