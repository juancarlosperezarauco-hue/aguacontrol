/* AquaControl - migrador SIG: ejecución, procedencia y severidad.
   Seguro para una base existente. No borra geometrías ni registros comerciales. */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.ImportacionesSIG','SourceFile') IS NULL
    ALTER TABLE dbo.ImportacionesSIG ADD SourceFile nvarchar(260) NOT NULL CONSTRAINT DF_ImportacionesSIG_SourceFile DEFAULT N'';
IF COL_LENGTH('dbo.ImportacionesSIG','Mode') IS NULL
    ALTER TABLE dbo.ImportacionesSIG ADD [Mode] nvarchar(12) NOT NULL CONSTRAINT DF_ImportacionesSIG_Mode DEFAULT N'APPEND';
IF COL_LENGTH('dbo.ImportacionesSIG','Status') IS NULL
    ALTER TABLE dbo.ImportacionesSIG ADD [Status] nvarchar(20) NOT NULL CONSTRAINT DF_ImportacionesSIG_Status DEFAULT N'COMPLETADA';
IF COL_LENGTH('dbo.ImportacionesSIG','UserId') IS NULL
    ALTER TABLE dbo.ImportacionesSIG ADD UserId int NULL;
IF COL_LENGTH('dbo.ImportacionesSIG','Omitted') IS NULL
    ALTER TABLE dbo.ImportacionesSIG ADD Omitted int NOT NULL CONSTRAINT DF_ImportacionesSIG_Omitted DEFAULT 0;
IF COL_LENGTH('dbo.ImportacionesSIG','Warnings') IS NULL
    ALTER TABLE dbo.ImportacionesSIG ADD Warnings int NOT NULL CONSTRAINT DF_ImportacionesSIG_Warnings DEFAULT 0;
IF COL_LENGTH('dbo.ImportacionesSIG','StartedAt') IS NULL
    ALTER TABLE dbo.ImportacionesSIG ADD StartedAt datetime2 NOT NULL CONSTRAINT DF_ImportacionesSIG_StartedAt DEFAULT SYSUTCDATETIME();
IF COL_LENGTH('dbo.ImportacionesSIG','CompletedAt') IS NULL
    ALTER TABLE dbo.ImportacionesSIG ADD CompletedAt datetime2 NULL;
IF COL_LENGTH('dbo.ImportacionesSIG','DurationMilliseconds') IS NULL
    ALTER TABLE dbo.ImportacionesSIG ADD DurationMilliseconds bigint NULL;
IF COL_LENGTH('dbo.IncidenciasSIG','Severity') IS NULL
    ALTER TABLE dbo.IncidenciasSIG ADD Severity nvarchar(12) NOT NULL CONSTRAINT DF_IncidenciasSIG_Severity DEFAULT N'ERROR';

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_ImportacionesSIG_Usuarios_UserId')
    ALTER TABLE dbo.ImportacionesSIG ADD CONSTRAINT FK_ImportacionesSIG_Usuarios_UserId FOREIGN KEY(UserId) REFERENCES dbo.Usuarios(IdUsuario);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.ImportacionesSIG') AND name='IX_ImportacionesSIG_UserId')
    CREATE INDEX IX_ImportacionesSIG_UserId ON dbo.ImportacionesSIG(UserId);

COMMIT TRANSACTION;
