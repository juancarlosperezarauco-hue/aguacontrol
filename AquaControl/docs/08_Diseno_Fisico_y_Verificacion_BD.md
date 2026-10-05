# Diseño físico y verificación de base de datos

## Decisiones físicas

- SQL Server almacena las capas SIG como `geometry` con SRID 4326.
- Cada capa conserva `ImportId`, `Ordinal` y `OriginalJson` para procedencia y trazabilidad.
- Los índices espaciales aceleran la consulta por extensión del visor.
- Índices únicos protegen usuarios, cuentas, conexiones, medidores, facturas, lecturas y claves naturales de importación.
- Restricciones verifican coordenadas, importes, cantidades y estados de órdenes.
- `Conexiones.FixedCodeId` mantiene la relación obligatoria con la cartografía; el índice único evita duplicidad de servicio sobre un Código Fijo.

## Scripts de actualización

| Script | Propósito | Efecto sobre datos |
|---|---|---|
| `00_Backup_Original.sql` | Respaldo previo | No modifica datos funcionales. |
| `01_AquaControl_Inicial.sql` | Esquema nuevo completo | Solo para base nueva. |
| `02_Importar_Legado.sql` | Conservación de usuarios/roles previos | No destruye la fuente original. |
| `04_Exigir_CodigoFijo_Conexion.sql` | Índice único de conexión por Código Fijo | Se bloquea si detecta duplicados. |
| `05_Completar_Bitacora_Migrador_SIG.sql` | Columnas y FK del migrador SIG | Idempotente, no borra registros. |
| `06_Verificar_PreDespliegue.sql` | Comprobación de estructura, índices y roles | Solo lectura. |

## Verificación realizada

El 04-10-2026 se aplicó `05_Completar_Bitacora_Migrador_SIG.sql` a la base aislada `AquaControlTests`. Luego se importaron las cuatro capas SIG para prueba y el flujo integral completó 17 verificaciones: autenticación, permisos, clientes, medición, facturación, avisos, órdenes, evidencias, materiales, auditoría y restricciones de estado.

La actualización se ejecutó sobre la base de pruebas, no sobre los SHP originales ni sobre datos comerciales reales.
