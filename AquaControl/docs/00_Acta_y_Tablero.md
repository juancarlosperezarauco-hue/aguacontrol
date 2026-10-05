# AquaControl — Acta y tablero de desarrollo

## Propósito

AquaControl gestiona abonados, conexiones, medición, facturación, saldos, avisos de corte, órdenes de trabajo y territorio SIG. Esta versión es operativa: no procesa cobros por QR, tarjeta ni otros pagos en línea. La información geográfica se conserva en SQL Server con geometrías WGS84 / EPSG:4326.

## Responsabilidades

| Rol | Responsabilidad principal |
|---|---|
| Super admin | Seguridad, catálogos, configuración y auditoría. |
| Administrador | Abonados, conexiones, medidores, facturación y reportes. |
| Supervisor | Órdenes, asignaciones, reprogramación y verificación. |
| Operario | Ejecución, actividades, materiales y evidencias. |
| Cliente | Consulta de contrato, consumo, facturas y avisos. |

## Acuerdos de inicio

- Los SHP originales son la fuente cartográfica y no se modifican desde AquaControl.
- La relación comercial obligatoria es **Conexión → Código Fijo SIG**; un Código Fijo no puede tener dos conexiones activas.
- La base principal es `AquaControlDev`; `AquaControlTests` se reserva para pruebas con datos identificados como ficticios.
- Antes de aplicar un script de actualización se realiza un respaldo SQL. Ninguna actualización incluida elimina clientes, geometrías o historial.

## Registro de avance de los puntos 1 a 5

| Punto | Producto verificable | Estado al 04-10-2026 |
|---|---|---|
| 1 | Acta y tablero | Completado en este documento. |
| 2 | Informe de diagnóstico SHP | `docs/06_Informe_Diagnostico_SIG.md` y reporte JSON generado. |
| 3 | Matriz de mapeo y diseño físico | `docs/02_Matriz_SHP_SQL.md` y `docs/08_Diseno_Fisico_y_Verificacion_BD.md`. |
| 4 | Arquitectura, casos de uso y estrategia Git | `docs/01_Arquitectura_y_Diagramas.md` y `docs/07_Casos_Uso_y_Prototipos.md`. |
| 5 | Scripts SQL verificados | `database/`, `database/06_Verificar_PreDespliegue.sql` y la prueba integral aislada. |

## Tablero de entregables

| Actividad | Estado | Evidencia |
|---|---|---|
| Inspección de cuatro SHP y WGS84 | Completada | `scripts/validate-sig.ps1` e informe SIG. |
| Matriz SHP a SQL | Completada | `docs/02_Matriz_SHP_SQL.md`. |
| Arquitectura, casos y Git | Completada | `docs/01_Arquitectura_y_Diagramas.md`. |
| Base de datos, restricciones, roles e índices | Completada | scripts de `database/` y validación `--verify`. |
| Dominio y solución web/API | Completada | proyectos Domain, Application, Infrastructure y API. |
| Migrador y bitácora | Completada | menú **Migrar SIG**. |
| Consultas GeoJSON, búsqueda y ficha | Completada | menú **Territorio y servicio**. |
| Mapa con capas, leyenda y estilos | Completada | Calles, clara, oscura, satélite y topográfico de Esri, sin clave API. |
| Modelo 3D operativo | Completada | Extrusión interactiva de manzanas, vías y conexiones desde las capas SIG. |
| Referencias OpenStreetMap | Completada | consulta limitada y con caché, sin clave API. |

## Conformidad

| Responsable | Revisión | Firma / fecha |
|---|---|---|
| Equipo de desarrollo | Alcance y evidencias técnicas | Pendiente de firma académica. |
| Docente o responsable de proyecto | Aprobación de avance | Pendiente. |

## Criterio de aceptación

Se considera aceptado cada módulo cuando compila, respeta permisos, registra auditoría cuando corresponde y supera las pruebas especificadas en `docs/03_Plan_Pruebas_SIG.md`.
