/* Amplía el estado de conexión sin modificar registros existentes.
   Requerido para valores operativos como PENDIENTE_INSTALACION. */
SET NOCOUNT ON;

IF OBJECT_ID('dbo.Conexiones','U') IS NULL
    THROW 51010, 'No existe la tabla dbo.Conexiones.', 1;

IF COL_LENGTH('dbo.Conexiones','Status') IS NULL
    THROW 51011, 'No existe la columna dbo.Conexiones.Status.', 1;

IF COL_LENGTH('dbo.Conexiones','Status') < 80
    ALTER TABLE dbo.Conexiones ALTER COLUMN [Status] nvarchar(40) NOT NULL;

PRINT 'Conexiones.Status verificado en nvarchar(40).';
