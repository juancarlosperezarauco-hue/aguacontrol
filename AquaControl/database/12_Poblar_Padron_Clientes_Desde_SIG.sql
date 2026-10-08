/*
  Padrón comercial a partir de la fuente SIG original.

  Fuente: Exp_CodigoFijo_4326 (Nombre y geometría importada en CodigosFijos).
  Alcance: crea Cliente, Cuenta de servicio, Conexión y Contrato por Código
  Fijo válido. La fuente no incluye serie, modelo ni lectura de medidor; por
  ello NO se inventan medidores. Esos equipos se registran después mediante
  la orden de instalación correspondiente.

  Seguridad de datos:
  - No elimina registros existentes.
  - Crea respaldos de cada tabla comercial demostrativa la primera vez.
  - Cierra contratos demostrativos, desactiva sus cuentas y libera su vínculo
    a Código Fijo antes de crear el padrón real.
  - Es idempotente: si ya existe el padrón SIG, sólo presenta el resumen.
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

DECLARE @Fecha datetime2(7) = SYSUTCDATETIME();
DECLARE @TarifaId int;

IF EXISTS (SELECT 1 FROM dbo.Clientes WHERE Document LIKE N'SIG-CF-%')
BEGIN
    SELECT
        N'El padrón SIG ya está cargado; no se realizaron cambios.' AS Resultado,
        (SELECT COUNT(*) FROM dbo.Clientes WHERE Document LIKE N'SIG-CF-%') AS ClientesSIG,
        (SELECT COUNT(*) FROM dbo.ContratosServicio c
          JOIN dbo.Clientes cl ON cl.Id=c.ClientId
          WHERE cl.Document LIKE N'SIG-CF-%' AND c.[End] IS NULL) AS ContratosVigentes;
    RETURN;
END;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.CodigosFijos
    WHERE NULLIF(LTRIM(RTRIM(Nombre)),N'') IS NOT NULL
      AND Longitud BETWEEN -180 AND 180
      AND Latitud BETWEEN -90 AND 90
)
    THROW 50040, 'No existen Códigos Fijos SIG válidos para poblar el padrón.', 1;

/* Respaldos de demostración. No se sobrescriben si el script se reintenta. */
IF OBJECT_ID(N'dbo.RespaldoPadronDemoAntesSIG_Clientes',N'U') IS NULL
    SELECT * INTO dbo.RespaldoPadronDemoAntesSIG_Clientes FROM dbo.Clientes;
IF OBJECT_ID(N'dbo.RespaldoPadronDemoAntesSIG_Cuentas',N'U') IS NULL
    SELECT * INTO dbo.RespaldoPadronDemoAntesSIG_Cuentas FROM dbo.CuentasServicio;
IF OBJECT_ID(N'dbo.RespaldoPadronDemoAntesSIG_Conexiones',N'U') IS NULL
    SELECT * INTO dbo.RespaldoPadronDemoAntesSIG_Conexiones FROM dbo.Conexiones;
IF OBJECT_ID(N'dbo.RespaldoPadronDemoAntesSIG_Contratos',N'U') IS NULL
    SELECT * INTO dbo.RespaldoPadronDemoAntesSIG_Contratos FROM dbo.ContratosServicio;
IF OBJECT_ID(N'dbo.RespaldoPadronDemoAntesSIG_Medidores',N'U') IS NULL
    SELECT * INTO dbo.RespaldoPadronDemoAntesSIG_Medidores FROM dbo.Medidores;
IF OBJECT_ID(N'dbo.RespaldoPadronDemoAntesSIG_Instalaciones',N'U') IS NULL
    SELECT * INTO dbo.RespaldoPadronDemoAntesSIG_Instalaciones FROM dbo.InstalacionesMedidor;

BEGIN TRANSACTION;
BEGIN TRY
    /* No se conoce una tarifa real en la fuente SIG; no se usa para facturar. */
    SELECT @TarifaId=Id
      FROM dbo.TarifaVersiones
     WHERE Name=N'Tarifa SIG pendiente de aprobación';

    IF @TarifaId IS NULL
    BEGIN
        INSERT dbo.TarifaVersiones(Name,Currency,FixedCharge,UnitPrice,[Start],Active)
        VALUES(N'Tarifa SIG pendiente de aprobación',N'BOB',0,0,@Fecha,0);
        SET @TarifaId=SCOPE_IDENTITY();
    END;

    DECLARE @Legado TABLE(
        ContractId int NOT NULL,
        AccountId int NOT NULL,
        ClientId int NOT NULL,
        ConnectionId int NOT NULL
    );

    /* Se conserva el historial de pruebas cerrando, nunca eliminando. */
    UPDATE c
       SET [End]=@Fecha
       OUTPUT inserted.Id,inserted.AccountId,inserted.ClientId,inserted.ConnectionId
         INTO @Legado(ContractId,AccountId,ClientId,ConnectionId)
      FROM dbo.ContratosServicio c
     WHERE c.[End] IS NULL;

    UPDATE a SET Active=0
      FROM dbo.CuentasServicio a
      JOIN @Legado l ON l.AccountId=a.Id;

    /* Libera la relación única Código Fijo -> Conexión para el padrón real. */
    UPDATE cn
       SET FixedCodeId=NULL,
           Status=N'ARCHIVADA'
      FROM dbo.Conexiones cn
      JOIN @Legado l ON l.ConnectionId=cn.Id;

    UPDATE cl SET Active=0
      FROM dbo.Clientes cl
      JOIN @Legado l ON l.ClientId=cl.Id;

    ;WITH Fuente AS (
        SELECT f.IdCodigo,f.CodFijo,f.Nombre,f.Longitud,f.Latitud,
               l.NroLote,m.UV,m.MZA
        FROM dbo.CodigosFijos f
        LEFT JOIN dbo.Lotes l ON l.IdLote=f.IdLote
        LEFT JOIN dbo.Manzanas m ON m.IdManzana=l.IdManzana
        WHERE NULLIF(LTRIM(RTRIM(f.Nombre)),N'') IS NOT NULL
          AND f.Longitud BETWEEN -180 AND 180
          AND f.Latitud BETWEEN -90 AND 90
    )
    INSERT dbo.Clientes(Name,Document,Email,Phone,Address,Active)
    SELECT LEFT(LTRIM(RTRIM(Nombre)),150),
           CONCAT(N'SIG-CF-',IdCodigo),
           N'',N'',
           LEFT(CONCAT(N'Código fijo ',COALESCE(CONVERT(nvarchar(30),CodFijo),CONVERT(nvarchar(30),IdCodigo)),
                       CASE WHEN NroLote IS NULL THEN N'' ELSE CONCAT(N' · Lote ',NroLote) END,
                       CASE WHEN UV IS NULL AND MZA IS NULL THEN N'' ELSE CONCAT(N' · UV ',COALESCE(UV,N'—'),N' / MZA ',COALESCE(MZA,N'—')) END),250),
           1
    FROM Fuente;

    INSERT dbo.CuentasServicio(Number,Active)
    SELECT CONCAT(N'AC-SIG-',f.IdCodigo),1
      FROM dbo.CodigosFijos f
     WHERE NULLIF(LTRIM(RTRIM(f.Nombre)),N'') IS NOT NULL
       AND f.Longitud BETWEEN -180 AND 180
       AND f.Latitud BETWEEN -90 AND 90;

    INSERT dbo.Conexiones(Code,FixedCodeId,SectorId,RoadId,Address,Longitude,Latitude,Status)
    SELECT CONCAT(N'CNX-SIG-',f.IdCodigo),f.IdCodigo,NULL,NULL,
           LEFT(CONCAT(N'Código fijo ',COALESCE(CONVERT(nvarchar(30),f.CodFijo),CONVERT(nvarchar(30),f.IdCodigo))),250),
           f.Longitud,f.Latitud,
           CASE f.Estado
             WHEN 2 THEN N'PENDIENTE_CORTE'
             WHEN 3 THEN N'CORTADA'
             WHEN 4 THEN N'INACTIVA'
             WHEN 5 THEN N'INACTIVA'
             ELSE N'ACTIVA'
           END
      FROM dbo.CodigosFijos f
     WHERE NULLIF(LTRIM(RTRIM(f.Nombre)),N'') IS NOT NULL
       AND f.Longitud BETWEEN -180 AND 180
       AND f.Latitud BETWEEN -90 AND 90;

    INSERT dbo.ContratosServicio(AccountId,ClientId,ConnectionId,TariffId,[Start],[End])
    SELECT a.Id,cl.Id,cn.Id,@TarifaId,@Fecha,NULL
      FROM dbo.CodigosFijos f
      JOIN dbo.Clientes cl ON cl.Document=CONCAT(N'SIG-CF-',f.IdCodigo)
      JOIN dbo.CuentasServicio a ON a.Number=CONCAT(N'AC-SIG-',f.IdCodigo)
      JOIN dbo.Conexiones cn ON cn.Code=CONCAT(N'CNX-SIG-',f.IdCodigo)
     WHERE NULLIF(LTRIM(RTRIM(f.Nombre)),N'') IS NOT NULL
       AND f.Longitud BETWEEN -180 AND 180
       AND f.Latitud BETWEEN -90 AND 90;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    N'Padrón SIG cargado. Los medidores permanecen pendientes de registro porque la fuente no contiene series.' AS Resultado,
    (SELECT COUNT(*) FROM dbo.Clientes WHERE Document LIKE N'SIG-CF-%') AS ClientesSIG,
    (SELECT COUNT(*) FROM dbo.CuentasServicio WHERE Number LIKE N'AC-SIG-%') AS AbonadosSIG,
    (SELECT COUNT(*) FROM dbo.Conexiones WHERE Code LIKE N'CNX-SIG-%') AS ConexionesSIG,
    (SELECT COUNT(*) FROM dbo.Medidores) AS MedidoresExistentes,
    (SELECT COUNT(*) FROM dbo.ContratosServicio c JOIN dbo.Clientes cl ON cl.Id=c.ClientId WHERE cl.Document LIKE N'SIG-CF-%' AND c.[End] IS NULL) AS ContratosSIGVigentes;
