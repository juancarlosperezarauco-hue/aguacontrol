# Carga de datos comerciales reales

Esta carpeta contiene el formato de intercambio para cargar el padrón real en AquaControl. Los archivos se entregan sin registros de ejemplo para no confundir datos de demostración con información oficial.

## Orden de carga

1. `01_clientes.csv`
2. `02_cuentas.csv`
3. `03_conexiones.csv`
4. `04_medidores.csv`
5. `05_instalaciones_medidor.csv`
6. `06_tarifas.csv`
7. `07_contratos.csv`

Guarde cada archivo como CSV UTF-8, separado por comas. No cambie los encabezados. Las fechas usan `AAAA-MM-DD`, los valores decimales usan punto y los valores lógicos son `true` o `false`.

## Reglas de seguridad e integridad

- `documento`, `numero_cuenta`, `codigo_conexion` y `serie` deben ser únicos dentro de su entidad.
- No incluya contraseñas, tarjetas, cuentas bancarias ni datos sensibles no requeridos.
- Una conexión puede referenciar el SIG existente mediante `codigo_fijo`, `id_sector` o `id_via`. Si no se dispone de esa referencia, se requieren `longitud` y `latitud` en WGS84 (SRID 4326).
- El código fijo debe coincidir con la columna `CodFijo` importada desde la capa SIG. Si hay más de un resultado, AquaControl detendrá esa fila para revisión.
- Los contratos relacionan cliente, cuenta, conexión y tarifa. Es la relación que habilita facturación y deuda.
- Los archivos no se aplican directamente a producción: primero se validan, se genera un reporte de errores y después se confirma la importación en una copia de respaldo.

## Columnas

| Archivo | Columnas requeridas |
|---|---|
| Clientes | `documento`, `nombre`, `telefono`, `email`, `direccion`, `activo` |
| Cuentas | `numero_cuenta`, `activo` |
| Conexiones | `codigo_conexion`, `codigo_fijo`, `id_sector`, `id_via`, `direccion`, `longitud`, `latitud`, `estado` |
| Medidores | `serie`, `modelo`, `activo` |
| Instalaciones | `codigo_conexion`, `serie`, `lectura_inicial`, `fecha_instalacion` |
| Tarifas | `nombre`, `moneda`, `cargo_fijo`, `precio_m3`, `fecha_inicio`, `activa` |
| Contratos | `documento`, `numero_cuenta`, `codigo_conexion`, `tarifa`, `fecha_inicio`, `fecha_fin` |

Los campos vacíos de `codigo_fijo`, `id_sector`, `id_via` y `fecha_fin` son aceptados cuando corresponda. Cada fila debe estar completa en las demás columnas requeridas.
