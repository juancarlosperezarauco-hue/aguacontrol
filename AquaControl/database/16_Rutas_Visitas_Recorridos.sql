/* Alcance SIG 2/2026. Incremento aditivo: no elimina ni rellena visitas/GPS ficticios.
   Problema: rutas no persistidas y GPS sin historial ni prueba de visita.
   Solución: rutas/paradas, visitas inmutables y posiciones de jornada con FK e índices.
   Impacto: la APK 0.3 exige visita GPS antes de efecto/finalización/no realizado.
   Respalde con COPY_ONLY,CHECKSUM y VERIFYONLY antes de aplicar en una BD existente.
   Rollback: volver al binario anterior conserva estas tablas. No borrarlas si contienen registros.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.JornadasCampo','U') IS NULL THROW 51016,'Ejecute primero el incremento 15.',1;
IF OBJECT_ID('dbo.RutasCampo','U') IS NULL
BEGIN
 CREATE TABLE dbo.RutasCampo (
 Id int IDENTITY PRIMARY KEY, OperatorId int NOT NULL REFERENCES dbo.Usuarios(IdUsuario),
 CreatedById int NOT NULL REFERENCES dbo.Usuarios(IdUsuario), CreatedAt datetime2 NOT NULL,
 Active bit NOT NULL, Kind nvarchar(20) NOT NULL, TravelMode nvarchar(20) NOT NULL,
 StartLatitude float NOT NULL, StartLongitude float NOT NULL, DistanceMeters float NOT NULL,
 NetworkHash nvarchar(64) NOT NULL, PathJson nvarchar(max) NOT NULL, Version rowversion NOT NULL,
 CONSTRAINT CK_Route_Json CHECK(ISJSON(PathJson)=1),
 CONSTRAINT CK_Route_Start CHECK(StartLatitude BETWEEN -90 AND 90 AND StartLongitude BETWEEN -180 AND 180 AND DistanceMeters>=0)
 );
 CREATE UNIQUE INDEX IX_RutasCampo_OperatorId ON dbo.RutasCampo(OperatorId) WHERE Active=1;
END;
IF OBJECT_ID('dbo.ParadasRuta','U') IS NULL
BEGIN
 CREATE TABLE dbo.ParadasRuta (
 Id int IDENTITY PRIMARY KEY, RouteId int NOT NULL REFERENCES dbo.RutasCampo(Id),
 OrderId int NOT NULL REFERENCES dbo.OrdenesServicio(IdOrden), AssignmentId int NOT NULL REFERENCES dbo.Asignaciones(Id),
 Sequence int NOT NULL, DistanceMeters float NOT NULL, AccessMeters float NOT NULL, Version rowversion NOT NULL,
 CONSTRAINT CK_Stop_Sequence CHECK(Sequence>0 AND DistanceMeters>=0 AND AccessMeters>=0)
 );
 CREATE UNIQUE INDEX IX_ParadasRuta_RouteId_Sequence ON dbo.ParadasRuta(RouteId,Sequence);
 CREATE UNIQUE INDEX IX_ParadasRuta_RouteId_OrderId ON dbo.ParadasRuta(RouteId,OrderId);
 CREATE INDEX IX_ParadasRuta_AssignmentId ON dbo.ParadasRuta(AssignmentId);
END;
IF OBJECT_ID('dbo.VisitasCampo','U') IS NULL
BEGIN
 CREATE TABLE dbo.VisitasCampo (
 Id int IDENTITY PRIMARY KEY, RequestId uniqueidentifier NOT NULL,
 OrderId int NOT NULL REFERENCES dbo.OrdenesServicio(IdOrden), UserId int NOT NULL REFERENCES dbo.Usuarios(IdUsuario),
 ShiftId int NOT NULL REFERENCES dbo.JornadasCampo(Id), MeterId int NULL REFERENCES dbo.Medidores(Id),
 EvidenceId int NULL REFERENCES dbo.Evidencias(Id), ObservedSerial nvarchar(80) NOT NULL, Reading decimal(18,4) NULL,
 Outcome nvarchar(20) NOT NULL, Observation nvarchar(2000) NOT NULL,
 Latitude float NOT NULL, Longitude float NOT NULL, Accuracy float NOT NULL, DistanceMeters float NOT NULL,
 CapturedAt datetime2 NOT NULL, ReceivedAt datetime2 NOT NULL, Version rowversion NOT NULL,
 CONSTRAINT CK_Visit_GPS CHECK(Latitude BETWEEN -90 AND 90 AND Longitude BETWEEN -180 AND 180 AND Accuracy>=0 AND DistanceMeters>=0),
 CONSTRAINT CK_Visit_Outcome CHECK(Outcome IN ('REALIZADO','OBSERVADO') AND (Reading IS NULL OR Reading>=0))
 );
 CREATE UNIQUE INDEX IX_VisitasCampo_RequestId ON dbo.VisitasCampo(RequestId);
 CREATE INDEX IX_VisitasCampo_OrderId_ReceivedAt ON dbo.VisitasCampo(OrderId,ReceivedAt);
 CREATE INDEX IX_VisitasCampo_UserId_ReceivedAt ON dbo.VisitasCampo(UserId,ReceivedAt);
END;
IF OBJECT_ID('dbo.PosicionesCampo','U') IS NULL
BEGIN
 CREATE TABLE dbo.PosicionesCampo (
 Id int IDENTITY PRIMARY KEY, ShiftId int NOT NULL REFERENCES dbo.JornadasCampo(Id),
 Latitude float NOT NULL, Longitude float NOT NULL, Accuracy float NOT NULL,
 CapturedAt datetime2 NOT NULL, ReceivedAt datetime2 NOT NULL, Version rowversion NOT NULL,
 CONSTRAINT CK_Track_GPS CHECK(Latitude BETWEEN -90 AND 90 AND Longitude BETWEEN -180 AND 180 AND Accuracy>=0)
 );
 CREATE UNIQUE INDEX IX_PosicionesCampo_ShiftId_CapturedAt ON dbo.PosicionesCampo(ShiftId,CapturedAt);
 CREATE INDEX IX_PosicionesCampo_CapturedAt ON dbo.PosicionesCampo(CapturedAt);
END;
COMMIT;
PRINT 'Incremento 16 verificado: rutas, paradas, visitas e historial GPS. Datos originales conservados.';
