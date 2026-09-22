-- Diagnóstico de solo lectura. Ejecutar en AquaControlDev o en su copia de validación.
-- No usar este script para corregir automáticamente geometrías o relaciones.
SET NOCOUNT ON;
SELECT DB_NAME() AS BaseActual;

SELECT OBJECT_NAME(i.object_id) AS Tabla, i.name AS Indice, i.is_disabled AS Deshabilitado,
       t.bounding_box_xmin, t.bounding_box_ymin, t.bounding_box_xmax, t.bounding_box_ymax
FROM sys.spatial_indexes i
JOIN sys.spatial_index_tessellations t ON t.object_id=i.object_id AND t.index_id=i.index_id
WHERE OBJECT_NAME(i.object_id) IN ('CodigosFijos','Lotes','Manzanas','Vias');

SELECT name AS Relacion, is_disabled AS Deshabilitada, is_not_trusted AS NoVerificada
FROM sys.foreign_keys
WHERE parent_object_id IN (OBJECT_ID('dbo.CodigosFijos'),OBJECT_ID('dbo.Lotes'),
    OBJECT_ID('dbo.Conexiones'),OBJECT_ID('dbo.ContratosServicio'));

SELECT 'Codigo sin lote existente' AS Problema, COUNT(*) AS Cantidad
FROM dbo.CodigosFijos c LEFT JOIN dbo.Lotes l ON l.IdLote=c.IdLote
WHERE c.IdLote IS NOT NULL AND l.IdLote IS NULL
UNION ALL
SELECT 'Lote sin manzana existente',COUNT(*)
FROM dbo.Lotes l LEFT JOIN dbo.Manzanas m ON m.IdManzana=l.IdManzana
WHERE l.IdManzana IS NOT NULL AND m.IdManzana IS NULL
UNION ALL
SELECT 'Conexion sin codigo existente',COUNT(*)
FROM dbo.Conexiones c LEFT JOIN dbo.CodigosFijos f ON f.IdCodigo=c.FixedCodeId
WHERE c.FixedCodeId IS NOT NULL AND f.IdCodigo IS NULL;

SELECT Id,Layer,Count AS Aceptados,Rejected AS Rechazados,Hash
FROM dbo.ImportacionesSIG ORDER BY Id;
