# AquaControl — validación SIG

Fecha de revisión: 14 de septiembre de 2026. Base: `AquaControlDev`.

## Resultado de la revisión

La carga de las cuatro capas originales está completa, con una geometría de lote rechazada y conservada en el archivo fuente para revisión. Los cuatro índices espaciales están creados y habilitados. Las relaciones existentes tienen claves foráneas habilitadas y verificadas; no se encontraron referencias huérfanas.

| Capa | Registros originales | Cargados | Rechazados | Vínculo existente | Sin coincidencia |
|---|---:|---:|---:|---:|---:|
| Código fijo | 6.271 | 6.271 | 0 | 5.118 con lote | 1.153 |
| Lotes | 15.281 | 15.280 | 1 | 9.578 con manzana | 5.702 |
| Manzanas | 863 | 863 | 0 | No aplica | No aplica |
| Vías | 578 | 578 | 0 | No aplica | No aplica |

Las geometrías cargadas presentan SRID 4326, validez geométrica y coordenadas dentro de rango. No hay geometrías cargadas vacías o nulas. La extensión de las cuatro capas está dentro del rectángulo de sus índices: oeste -61,1; sur -16,5; este -60,8; norte -16,2.

## Problemas encontrados y tratamiento

### Índices incompletos

**PROBLEMA:** solo existía `SIX_CodigosFijos_Geom`; faltaban los índices de lotes, manzanas y vías.

**CAUSA:** la ejecución anterior de creación de índices agotó el tiempo de espera de 30 segundos.

**SOLUCIÓN:** se completó la importación administrativa con un tiempo máximo de 300 segundos por comando y verificación de existencia de cada índice.

**CAMBIO EN BD:** creación de `SIX_Lotes_Geom`, `SIX_Manzanas_Geom` y `SIX_Vias_Geom`. La operación no reemplaza las tablas ni geometrías.

**IMPACTO:** consultas espaciales por extensión con soporte de índices. Las cuatro capas ya responden por la API. Para revertir exclusivamente este cambio se pueden retirar esos tres índices; no se requiere revertir datos ni relaciones. No se ejecutó esa reversión.

### Códigos y lotes sin coincidencia

**PROBLEMA:** 1.153 códigos no intersectan ningún lote cargado; el punto interior de 5.702 lotes no intersecta ninguna manzana cargada.

**CAUSA:** las coberturas geométricas suministradas no permiten obtener una coincidencia con la regla definida. Determinar si se debe a diferencias de levantamiento, límites incompletos o errores de ubicación requiere revisión catastral.

**SOLUCIÓN:** conservarlos sin vínculo y entregar sus identificadores, filas originales y coordenadas para revisión. No se asignaron al polígono más cercano ni se desplazaron puntos.

**CAMBIO EN BD:** ninguno por esta incidencia. No se detectaron casos con múltiples candidatos ni vínculos únicos pendientes de aplicar.

**IMPACTO:** esos objetos se muestran en el mapa, pero su relación lote/manzana queda pendiente. El JSON contiene todos los casos bajo `issues`, con motivos `SIN_COINCIDENCIA`.

### Cobertura parcial de lotes vinculados

**PROBLEMA:** 5.610 de los 9.578 lotes vinculados no quedan totalmente cubiertos por la geometría de su manzana.

**CAUSA:** la asociación actual exige coincidencia única del punto interior del lote; no exige que todos sus vértices estén dentro de la manzana. Pequeñas diferencias de límites también pueden producir cobertura parcial.

**SOLUCIÓN:** mantener el vínculo como asociación por punto interior y señalar que no es certificación catastral. Antes de utilizarlo como límite legal, revisar los polígonos contra la fuente autorizada.

**CAMBIO EN BD:** ninguno; no se corrigen ni recortan los polígonos automáticamente.

**IMPACTO:** los IDs de los lotes están enumerados en `partialLotCoverage.lotIds`. Este grupo es distinto del grupo de lotes sin vínculo y no se debe sumar como si fueran referencias huérfanas.

### Calidad de las fuentes

**PROBLEMA:** el lote de fila original 4.916 tiene geometría inválida; en 6.265 códigos los atributos Longi/Latid difieren de la geometría SHP.

**CAUSA:** inconsistencias presentes en las fuentes originales.

**SOLUCIÓN:** el lote inválido permanece fuera de la capa operativa y registrado en `IncidenciasSIG`. Para los códigos, el mapa utiliza la geometría y conserva los atributos originales en `OriginalJson`.

**CAMBIO EN BD:** se mantiene el tratamiento de la importación anterior; esta revisión no realizó reparaciones automáticas.

**IMPACTO:** una eventual corrección debe producir una nueva versión de la fuente y una conciliación controlada. Los 41 archivos del inventario original mantienen su SHA256 sin cambios.

## Mapa y API

- Apertura en `http://localhost:5080/#map`, contra la base con datos SIG. El puerto 5081 corresponde al entorno de pruebas operativas.
- Resumen por capa y aviso de elementos sin vínculo. Ajuste de vista a las extensiones de las geometrías.
- Manzanas y vías en la vista general; lotes y códigos fijos desde zoom 16 al activar las casillas.
- Consultas GeoJSON limitadas a la extensión visible, con aviso cuando se alcanza el límite de 1.000 elementos por capa.
- Detalle con ID y relación a lote/manzana; la relación de lote indica que se basa en punto interior.
- Fondo local y escala de distancia. Se retiraron las teselas externas porque el proveedor devolvía imágenes de bloqueo; la cartografía visible procede de los datos suministrados.
- La pantalla no ejecuta la auditoría completa de validez geométrica; esa tarea se realiza con `validate-sig.ps1`.

## Relaciones con clientes y conexiones

El modelo dispone de `Conexiones.FixedCodeId → CodigosFijos.IdCodigo → Lotes.IdLote → Manzanas.IdManzana`, además de `Conexiones.RoadId → Vias.IdVia`. Los clientes se relacionan con las conexiones mediante `ContratosServicio`.

En `AquaControlDev` hay cero clientes, conexiones y contratos al realizar esta auditoría. Por ello se verificó la integridad estructural, pero todavía no se puede certificar una correspondencia con abonados reales. No se inventaron clientes para completar la cartografía. Cuando se incorporen, deberá comprobarse la correspondencia de cada conexión con su código fijo.

## Evidencia y repetición

- `Validacion_SIG.json`: auditoría completa, totales, extensiones, incidencias de importación, pendientes y cobertura parcial.
- `Verificacion_SQL_SIG.txt`: índices habilitados, límites de indexación, claves foráneas y ausencia de referencias huérfanas.
- `Pruebas_SIG_API.json`: 15 peticiones verificadas, incluyendo autenticación, las cuatro capas, áreas vacías y entradas inválidas. No se crean registros de negocio.
- Compilación del backend: cero errores y cero advertencias. Sintaxis de JavaScript verificada con `node --check`.

Comandos de repetición e interpretación en `AquaControl/README.md`, sección «Validación SIG y mapa».

Se observaron tiempos de espera intermitentes del controlador SQL durante las primeras aperturas, incluso en consultas de permisos. La auditoría y las pruebas posteriores terminaron correctamente; no se ha demostrado todavía estabilidad prolongada bajo carga. La opción de red administrada del controlador se revisó, pero no se activó porque Microsoft la documenta para pruebas y con limitaciones de autenticación: [documentación oficial de SqlClient](https://learn.microsoft.com/en-us/sql/connect/ado-net/appcontext-switches?view=sql-server-ver17#enable-managed-networking-on-windows).

La validación técnica identifica los pendientes catastrales; no sustituye su revisión por la entidad responsable de los datos.
