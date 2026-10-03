# AquaControl — Acta y tablero de desarrollo

## Propósito

AquaControl gestiona abonados, conexiones, medición, facturación, cobros simulados, avisos de corte, órdenes de trabajo y territorio SIG. La información geográfica se conserva en SQL Server con geometrías WGS84 / EPSG:4326.

## Responsabilidades

| Rol | Responsabilidad principal |
|---|---|
| Super admin | Seguridad, catálogos, configuración y auditoría. |
| Administrador | Abonados, conexiones, medidores, facturación y reportes. |
| Supervisor | Órdenes, asignaciones, reprogramación y verificación. |
| Operario | Ejecución, actividades, materiales y evidencias. |
| Cliente | Consulta de contrato, consumo, facturas y avisos. |

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
| Referencias OpenStreetMap | Completada | consulta limitada y con caché, sin clave API. |

## Criterio de aceptación

Se considera aceptado cada módulo cuando compila, respeta permisos, registra auditoría cuando corresponde y supera las pruebas especificadas en `docs/03_Plan_Pruebas_SIG.md`.
