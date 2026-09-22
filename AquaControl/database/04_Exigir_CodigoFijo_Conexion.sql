/* AquaControl: ejecutar una vez después de resolver conexiones duplicadas o sin Código Fijo.
   No elimina datos y bloquea que un mismo Código Fijo tenga dos conexiones. */
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
IF EXISTS (SELECT 1 FROM dbo.Conexiones GROUP BY FixedCodeId HAVING FixedCodeId IS NOT NULL AND COUNT(*) > 1)
    THROW 51000, 'Hay Códigos Fijos asociados a más de una conexión. Corrija antes de aplicar esta migración.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Conexiones_CodigoFijo' AND object_id = OBJECT_ID('dbo.Conexiones'))
    CREATE UNIQUE INDEX UX_Conexiones_CodigoFijo ON dbo.Conexiones(FixedCodeId) WHERE FixedCodeId IS NOT NULL;
