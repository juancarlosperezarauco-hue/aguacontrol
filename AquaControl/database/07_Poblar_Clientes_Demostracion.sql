/*
  AquaControl - datos ficticios para demostración.
  Crea 8 clientes, cuentas, conexiones, medidores, instalaciones y contratos.
  Requiere los Códigos Fijos SIG previamente importados.
  No contiene personas ni datos reales.
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

DECLARE @Fecha datetime2 = '2026-01-01T00:00:00Z';
DECLARE @TarifaId int;
DECLARE @UsuarioSistema int = (SELECT TOP (1) IdUsuario FROM Usuarios WHERE Login = 'aquadmin');

DECLARE @Poblacion TABLE (
    Orden int PRIMARY KEY,
    Documento nvarchar(40) NOT NULL,
    Nombre nvarchar(150) NOT NULL,
    Telefono nvarchar(40) NOT NULL,
    Correo nvarchar(100) NOT NULL,
    Direccion nvarchar(250) NOT NULL,
    Cuenta nvarchar(40) NOT NULL,
    Conexion nvarchar(40) NOT NULL,
    CodigoFijo int NOT NULL,
    Estado nvarchar(40) NOT NULL,
    Serie nvarchar(50) NULL,
    Modelo nvarchar(100) NULL,
    LecturaInicial decimal(18,4) NULL
);

INSERT @Poblacion VALUES
(1,'DEMO-CL-0101','Usuario Demostración 01','+59170001001','usuario01@example.invalid','Av. SIG Demostración 101','DEMO-AC-0101','DEMO-CNX-0101',5788,'ACTIVA','DEMO-MED-0101','Medidor demostración 15mm',145.20),
(2,'DEMO-CL-0102','Usuario Demostración 02','+59170001002','usuario02@example.invalid','Av. SIG Demostración 102','DEMO-AC-0102','DEMO-CNX-0102',5185,'CORTADA','DEMO-MED-0102','Medidor demostración 15mm',98.60),
(3,'DEMO-CL-0103','Usuario Demostración 03','+59170001003','usuario03@example.invalid','Av. SIG Demostración 103','DEMO-AC-0103','DEMO-CNX-0103',2055,'ACTIVA','DEMO-MED-0103','Medidor demostración 20mm',210.00),
(4,'DEMO-CL-0104','Usuario Demostración 04','+59170001004','usuario04@example.invalid','Av. SIG Demostración 104','DEMO-AC-0104','DEMO-CNX-0104',5187,'PENDIENTE_INSTALACION',NULL,NULL,NULL),
(5,'DEMO-CL-0105','Usuario Demostración 05','+59170001005','usuario05@example.invalid','Av. SIG Demostración 105','DEMO-AC-0105','DEMO-CNX-0105',4871,'ACTIVA','DEMO-MED-0105','Medidor demostración 15mm',75.30),
(6,'DEMO-CL-0106','Usuario Demostración 06','+59170001006','usuario06@example.invalid','Av. SIG Demostración 106','DEMO-AC-0106','DEMO-CNX-0106',6590,'ACTIVA','DEMO-MED-0106','Medidor demostración 20mm',180.10),
(7,'DEMO-CL-0107','Usuario Demostración 07','+59170001007','usuario07@example.invalid','Av. SIG Demostración 107','DEMO-AC-0107','DEMO-CNX-0107',4257,'CORTADA','DEMO-MED-0107','Medidor demostración 15mm',62.80),
(8,'DEMO-CL-0108','Usuario Demostración 08','+59170001008','usuario08@example.invalid','Av. SIG Demostración 108','DEMO-AC-0108','DEMO-CNX-0108',3625,'PENDIENTE','DEMO-MED-0108','Medidor demostración 15mm',34.40);

IF EXISTS (SELECT 1 FROM Clientes c JOIN @Poblacion p ON p.Documento=c.Document)
    THROW 50001, 'La población demostrativa ya fue cargada o existe un documento DEMO-CL repetido.', 1;
IF EXISTS (SELECT 1 FROM CuentasServicio c JOIN @Poblacion p ON p.Cuenta=c.Number)
    THROW 50002, 'Existe una cuenta de abonado demostrativa.', 1;
IF EXISTS (SELECT 1 FROM Conexiones c JOIN @Poblacion p ON p.Conexion=c.Code)
    THROW 50003, 'Existe una conexión demostrativa.', 1;
IF EXISTS (SELECT 1 FROM Medidores m JOIN @Poblacion p ON p.Serie=m.Serial)
    THROW 50004, 'Existe un medidor demostrativo.', 1;
IF (SELECT COUNT(*) FROM CodigosFijos f JOIN @Poblacion p ON p.CodigoFijo=f.CodFijo) <> 8
    THROW 50005, 'Faltan Códigos Fijos SIG requeridos o alguno es ambiguo.', 1;
IF EXISTS (SELECT 1 FROM Conexiones c JOIN CodigosFijos f ON f.IdCodigo=c.FixedCodeId JOIN @Poblacion p ON p.CodigoFijo=f.CodFijo)
    THROW 50006, 'Uno de los Códigos Fijos SIG de demostración ya está asociado a otra conexión.', 1;

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
    SELECT Nombre,Documento,Correo,Telefono,Direccion,1 FROM @Poblacion;

    INSERT CuentasServicio (Number,Active)
    SELECT Cuenta,1 FROM @Poblacion;

    INSERT Conexiones (Code,FixedCodeId,SectorId,RoadId,Address,Longitude,Latitude,Status)
    SELECT p.Conexion,f.IdCodigo,NULL,NULL,p.Direccion,f.Longitud,f.Latitud,p.Estado
    FROM @Poblacion p
    JOIN CodigosFijos f ON f.CodFijo=p.CodigoFijo;

    INSERT Medidores (Serial,Model,Active)
    SELECT Serie,Modelo,1 FROM @Poblacion WHERE Serie IS NOT NULL;

    INSERT InstalacionesMedidor (ConnectionId,MeterId,Start,[End],InitialReading,FinalReading)
    SELECT c.Id,m.Id,@Fecha,NULL,p.LecturaInicial,NULL
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
    VALUES (@UsuarioSistema,'poblacion.demo','clientes','Se cargaron 8 registros ficticios de clientes, conexiones y medidores.',SYSUTCDATETIME());

    COMMIT TRANSACTION;
    SELECT 'Población demostrativa creada: 8 clientes, 8 cuentas, 8 conexiones, 7 medidores y 8 contratos.' AS Resultado;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
