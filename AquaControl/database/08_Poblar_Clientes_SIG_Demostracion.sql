/*
  Amplía el mapa SIG con 40 clientes completamente ficticios.
  Los puntos se asignan a Códigos Fijos disponibles, sin reemplazar
  conexiones reales ni datos catastrales originales.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @Cantidad int = 40;
DECLARE @Fecha datetime2 = '2026-01-01T00:00:00Z';
DECLARE @TarifaId int;
DECLARE @UsuarioSistema int = (SELECT TOP (1) IdUsuario FROM Usuarios WHERE Login = 'aquadmin');

IF EXISTS (SELECT 1 FROM Clientes WHERE Document LIKE 'DEMO-SIG-%')
    THROW 50020, 'La población SIG demostrativa ya fue cargada.', 1;

DECLARE @Poblacion TABLE (
    Orden int PRIMARY KEY,
    CodigoId int NOT NULL,
    Documento nvarchar(40) NOT NULL,
    Nombre nvarchar(150) NOT NULL,
    Cuenta nvarchar(40) NOT NULL,
    Conexion nvarchar(40) NOT NULL,
    Serie nvarchar(50) NULL,
    Estado nvarchar(40) NOT NULL
);

;WITH CodigosDisponibles AS (
    SELECT TOP (@Cantidad)
        ROW_NUMBER() OVER (ORDER BY ABS(CHECKSUM(CONVERT(nvarchar(20),f.IdCodigo)))) AS Orden,
        f.IdCodigo
    FROM CodigosFijos f
    WHERE f.Longitud IS NOT NULL AND f.Latitud IS NOT NULL
      AND f.CodFijo IS NOT NULL
      AND (SELECT COUNT(*) FROM CodigosFijos u WHERE u.CodFijo=f.CodFijo)=1
      AND NOT EXISTS (SELECT 1 FROM Conexiones c WHERE c.FixedCodeId=f.IdCodigo)
    ORDER BY ABS(CHECKSUM(CONVERT(nvarchar(20),f.IdCodigo)))
)
INSERT @Poblacion (Orden,CodigoId,Documento,Nombre,Cuenta,Conexion,Serie,Estado)
SELECT Orden,IdCodigo,
       CONCAT('DEMO-SIG-',RIGHT(CONCAT('0000',Orden),4)),
       CONCAT('Usuario SIG Demostración ',RIGHT(CONCAT('0000',Orden),4)),
       CONCAT('DEMO-SIG-AC-',RIGHT(CONCAT('0000',Orden),4)),
       CONCAT('DEMO-SIG-CNX-',RIGHT(CONCAT('0000',Orden),4)),
       CASE WHEN Orden%10=1 THEN NULL ELSE CONCAT('DEMO-SIG-MED-',RIGHT(CONCAT('0000',Orden),4)) END,
       CASE WHEN Orden%10 IN (3,8) THEN 'CORTADA'
            WHEN Orden%10 IN (1,6) THEN 'PENDIENTE_INSTALACION'
            WHEN Orden%10=4 THEN 'PENDIENTE'
            ELSE 'ACTIVA' END
FROM CodigosDisponibles;

IF (SELECT COUNT(*) FROM @Poblacion)<>@Cantidad
    THROW 50021, 'No hay suficientes Códigos Fijos SIG disponibles para poblar el mapa.', 1;

BEGIN TRANSACTION;
BEGIN TRY
    SELECT @TarifaId=Id FROM TarifaVersiones WHERE Name='Tarifa demostración población 2026';
    IF @TarifaId IS NULL
    BEGIN
        INSERT TarifaVersiones (Name,Currency,FixedCharge,UnitPrice,Start,Active)
        VALUES ('Tarifa demostración población 2026','BOB',12.00,2.50,@Fecha,1);
        SET @TarifaId=SCOPE_IDENTITY();
    END;

    INSERT Clientes (Name,Document,Email,Phone,Address,Active)
    SELECT Nombre,Documento,CONCAT(LOWER(REPLACE(Documento,'-','.')),'@example.invalid'),CONCAT('+591710',RIGHT(CONCAT('0000',Orden),4)),CONCAT('Ubicación SIG demostrativa ',Orden),1
    FROM @Poblacion;

    INSERT CuentasServicio (Number,Active)
    SELECT Cuenta,1 FROM @Poblacion;

    INSERT Conexiones (Code,FixedCodeId,SectorId,RoadId,Address,Longitude,Latitude,Status)
    SELECT p.Conexion,p.CodigoId,NULL,NULL,CONCAT('Ubicación SIG demostrativa ',p.Orden),f.Longitud,f.Latitud,p.Estado
    FROM @Poblacion p
    JOIN CodigosFijos f ON f.IdCodigo=p.CodigoId;

    INSERT Medidores (Serial,Model,Active)
    SELECT Serie,'Medidor SIG demostración 15mm',1 FROM @Poblacion WHERE Serie IS NOT NULL;

    INSERT InstalacionesMedidor (ConnectionId,MeterId,Start,[End],InitialReading,FinalReading)
    SELECT c.Id,m.Id,@Fecha,NULL,CAST(50+p.Orden*3 AS decimal(18,4)),NULL
    FROM @Poblacion p
    JOIN Conexiones c ON c.Code=p.Conexion
    JOIN Medidores m ON m.Serial=p.Serie
    WHERE p.Serie IS NOT NULL;

    INSERT ContratosServicio (AccountId,ClientId,ConnectionId,TariffId,Start,[End])
    SELECT a.Id,c.Id,cn.Id,@TarifaId,@Fecha,NULL
    FROM @Poblacion p
    JOIN Clientes c ON c.Document=p.Documento
    JOIN CuentasServicio a ON a.Number=p.Cuenta
    JOIN Conexiones cn ON cn.Code=p.Conexion;

    INSERT Bitacora (UserId,Action,Resource,Detail,CreatedAt)
    VALUES (@UsuarioSistema,'poblacion.demo.sig','clientes','Se cargaron 40 clientes ficticios vinculados al SIG.',SYSUTCDATETIME());

    COMMIT TRANSACTION;
    SELECT 'Población SIG demostrativa creada: 40 clientes y 40 puntos de conexión.' AS Resultado;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
