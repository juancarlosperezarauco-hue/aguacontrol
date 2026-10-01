# Matriz de mapeo SHP → SQL Server

| Archivo fuente | Tabla de destino | Geometría | Campos reutilizados | Relación resultante |
|---|---|---|---|---|
| `Exp_CodigoFijo_4326` | `CodigosFijos` | Punto | `CodF_SQL`, `CodF_SIG`, `CodFijo`, `Nombre` | Se vincula a un lote y puede ser referencia de una conexión. |
| `Exp_MapaBase_LOTES_4326` | `Lotes` | Polígono | `Id`, `NroLote` | Se vincula a una manzana por punto interior. |
| `Exp_MapaBase_MZA_4326` | `Manzanas` | Polígono | `Id`, `UV_MZA`, `UV`, `MZA` | Contiene lotes. |
| `Exp_MapaBase_VIAS_4326` | `Vias` | Línea | `OBJECTID`, `Nombre`, `type`, `OSMID` | Referencia visual y de dirección para conexiones. |

## Reglas de carga

1. El migrador exige `.shp`, `.shx`, `.dbf` y `.prj`, codificación UTF-8 y WGS84 geográfico.
2. Conserva los atributos completos de origen en `OriginalJson`.
3. `APPEND` usa una clave natural para no duplicar registros; `REPLACE` se bloquea si existen conexiones asociadas.
4. La geometría se guarda con SRID 4326 y se crean índices espaciales por capa.
5. La bitácora de importación conserva archivo, hash, modo, cantidades, advertencias y errores.
