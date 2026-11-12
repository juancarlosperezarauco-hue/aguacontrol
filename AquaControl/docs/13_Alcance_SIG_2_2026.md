# Aplicación del alcance SIG 2/2026

Fuentes leídas: `INF 442  SIG 2026-10-15 13_30(GMT-4_00).docx` y las 19 diapositivas de `AlcanceProyecto_2_2026.pptx`. Las capturas son referencias funcionales; sus cantidades, clientes y credenciales no se copiaron a la BD operativa.

## Trazabilidad

| Requisito | Implementación | Verificación / límite |
|---|---|---|
| Web y APK con autenticación | Login existente, sesiones, CSRF, cambio de contraseña temporal y perfiles de campo | Pruebas de roles e inicio web en BD exclusiva de pruebas |
| Dashboard general | Menú Dashboard: conteos y porcentajes reales por estado, órdenes, jornadas, visitas y rutas | API SQL; cero filas produce cero, sin cifras de muestra |
| Menú operativo | Dashboard, Asignaciones y rutas, Monitoreo, Órdenes, Capas y búsqueda SIG, usuario y Salir | Se mantiene el visor unificado solicitado previamente |
| Normal, para corte, cortado, baja parcial/total | Filtro de estado y colores; bajas administrativas con motivo, versión y auditoría | No se inventan bajas ni se cambia el estado original del SHP |
| Búsqueda por código fijo / cliente | API con búsquedas y filtros explícitos, hasta 250 resultados | El visor empieza sin puntos; afinar la búsqueda cuando alcance el límite |
| Manzanas, lotes y vías | Capas SIG originales, GeoJSON por extensión, leyenda y atributos | No se reemplazan los datos originales |
| Dimensiones de manzanas / lotes | Superficie, perímetro y longitudes al identificar la entidad | Aproximación geográfica operativa, no un plano catastral certificado |
| Fondos y 2D / 3D | Sin fondo, claro, oscuro, satélite, topográfico; modelo Three.js con vista superior y puntos de los resultados | 3D es extrusión esquemática; no contiene alturas reales ni reconstrucción fotogramétrica |
| Cortador / reconector | Recorridos CORTE y RECONEXION para el perfil OPERARIO TECNICO | Se respeta la jerarquía aprobada de cinco roles; la especialidad se elige en el trabajo, no se duplican roles |
| Cantidad y partida | Selector de operario, pendientes actuales, cantidad adicional, modo y partida por clic | Usa órdenes existentes; no crea cortes solo por seleccionar puntos del padrón |
| Dijkstra sobre vías | Grafo de `dbo.Vias`, nodos en intersecciones, distancias en metros, sentidos únicos y cruces de puente/túnel | Pruebas de ruta en U, sentidos, cruce, red desconectada y capa SQL real |
| Ruta sin cruzar manzanas | La línea azul usa exclusivamente aristas de vías | Acceso final del eje vial al medidor informado aparte; máximo 150 m |
| Guardar y recibir ruta | RutasCampo + ParadasRuta, asignaciones atómicas, orden numerado y geometría en APK | Huella de red y versiones impiden guardar una previsualización obsoleta |
| Monitoreo en tiempo real | Jornada voluntaria del operario, actualización de pantalla cada 10 s, precisión y antigüedad visibles | GPS / Internet necesarios; posición antigua se marca SIN SEÑAL |
| Historial de recorrido | PosicionesCampo; selección de operario / día UTC, visitas y control de reproducción por posición | Línea punteada une muestras, con cortes ante huecos >60 s; no prueba el camino exacto entre envíos |
| Ubicación actual, destino, distancia | APK muestra coordenadas y distancia directa, más distancia vial de cada parada | Navegación externa recalcula desde la posición real |
| Navegar a pie, moto o vehículo | Lanzamiento de Google Maps con modo y solicitud de giro a giro | Moto y navegación dependen de cobertura y disponibilidad de Google Maps; sin API key propia |
| Foto del medidor | Cámara / galería, foto ligada por servidor a orden, conexión, instalación y serie | Finalización exige foto del medidor actual |
| Número de medidor y lectura de corte / reconexión | Visita REALIZADO exige serie observada, lectura no decreciente y fotografía | Lectura técnica queda en visita; no genera otra factura de consumo |
| Casa cerrada / observado | Visita OBSERVADO con motivo y GPS; NO_REALIZADA exige esa visita | No permite NO_REALIZADA después de registrar un efecto físico |
| Fecha, hora y GPS al guardar | Captura del dispositivo, recepción UTC del servidor, precisión y distancia al destino | Jornada propia, posición previamente transmitida y fresca; no acepta coordenadas ausentes |
| OCR opcional | Entrada manual del número del medidor | OCR no incorporado; no condiciona el alcance obligatorio |
| Entrega completa | Código organizado, scripts inicial/incrementales, manual y compilación APK debug | El código y los scripts se versionan en Git; la APK y los respaldos privados se generan y entregan aparte |

## Decisiones técnicas y de datos

**PROBLEMA:** la ruta anterior usaba cercanía y dibujaba segmentos rectos entre propiedades. **CAUSA:** no existía un grafo vial ni persistencia del recorrido. **SOLUCIÓN:** noding de líneas con NetTopologySuite, conservación de dirección `oneway`, separación de interiores puente/túnel y Dijkstra. **CAMBIO EN BD:** RutasCampo y ParadasRuta con claves foráneas a Usuarios, OrdenesServicio y Asignaciones. **IMPACTO:** la APK recibe un orden estable; las redes desconectadas se reportan en lugar de dibujar atajos.

Dijkstra garantiza el menor costo **de cada tramo** en el grafo. El orden de múltiples visitas usa vecino más cercano por distancia vial; no es una solución exacta del problema del viajante. Se seleccionan como máximo 50 paradas; las adicionales se toman de hasta 200 órdenes sin asignar, priorizando cercanía a la partida. No se promete un mínimo global ni se interpretan restricciones viales ausentes del SIG. La red usa proyección local métrica y noding con precisión de 0,1 m. Deben revisarse cruces a distinto nivel y restricciones de acceso antes de uso productivo.

**PROBLEMA:** una observación podía guardarse sin demostrar presencia. **CAUSA:** se conservaba solo la última posición, sin registro de visita. **SOLUCIÓN:** visita inmutable con resultado, GPS fresco, fotografía y datos observados del medidor. **CAMBIO EN BD:** VisitasCampo y PosicionesCampo, FK e índices. **IMPACTO:** requiere APK 0.3; una APK anterior puede consultar trabajos pero no completar el nuevo flujo de visita.

No se borran tablas, clientes, medidores, fotos o geometrías. Las rutas anteriores quedan archivadas al guardar otra. Las asignaciones históricas se conservan. Una visita no altera automáticamente facturas ni lecturas comerciales.

## Flujo del supervisor y operario

1. Crear las órdenes con conexión/cliente y ubicación. Los cortes mantienen aviso y reglas de deuda. Las reconexiones requieren un servicio cortado al confirmar su efecto.
2. Abrir **Asignaciones y rutas**, elegir CORTE, RECONEXION o LECTURA y el operario de la especialidad correspondiente.
3. Revisar pendientes y cantidad adicional; marcar la partida en el mapa y elegir traslado.
4. Calcular, revisar paradas y advertencias, y pulsar **Guardar asignación**.
5. Operario inicia sesión en APK 0.3, inicia jornada con permisos GPS y actualiza trabajos. Verá números y trazado guardados.
6. Abrir parada, navegar y comenzar trabajo. Subir fotografía del medidor actual.
7. Guardar visita **REALIZADO** con serie y lectura, o **OBSERVADO** con motivo. La posición debe haberse enviado desde la jornada propia.
8. Para corte/reconexión, confirmar el efecto físico realizado; el corte requiere autorización en línea vigente. Completar actividades y finalizar. Si no se pudo realizar, marcar NO_REALIZADA después de guardar OBSERVADO.
9. Supervisor verifica y cierra; consulta **Monitoreo de campo** para posiciones e historial.

## Configuración y actualización

Con una BD existente, detener AquaControl y ejecutar desde la carpeta AquaControl:

```powershell
.\scripts\upgrade-scope.ps1 -SqlServer 'localhost' -Database 'AquaControlDev'
.\scripts\run-mobile.ps1 -Port 5080
```

El script verifica un respaldo COPY_ONLY/CHECKSUM en la carpeta de backups de SQL Server y aplica los incrementos 15 y 16. Requiere `sqlcmd`, SDK .NET 10 y permisos de respaldo/DDL. Si usa una instancia diferente, ajustar `-SqlServer` y la cadena de conexión del servidor. Una instalación nueva mediante `scripts/setup.ps1 -Initialize` utiliza el modelo completo.

Valores operativos iniciales (configurables en el servidor):

```text
FieldVisit__MaxDistanceMeters=150
FieldVisit__MaxAccuracyMeters=100
FieldVisit__MaxAgeSeconds=60
```

No requieren reiniciar la APK; sí el backend tras cambiar configuración. La API rechaza valores fuera de límites de seguridad. La posición debe coincidir con la última posición ya transmitida por esa jornada. Esto verifica coherencia y presencia declarada por el dispositivo; no es una certificación contra GPS manipulado.

Guardar observaciones web con geolocalización requiere navegador con contexto seguro (localhost o HTTPS). Para un teléfono en LAN, utilizar la APK debug; la versión release debe usar HTTPS. No exponer la instancia Development directamente a Internet.

Rollback de aplicación: recuperar el binario anterior y conservar las cuatro tablas nuevas; no borrarlas si contienen historial. Restaurar el respaldo solo durante mantenimiento y después de conservar los registros posteriores. La migración es aditiva y repetible.

## Pruebas y aceptación

```powershell
dotnet run --project tests/Scope -- --real-network
dotnet run --project tests/Roles
```

Scope prueba algoritmos sin mutar la BD operativa; `--real-network` realiza además una consulta de solo lectura a AquaControlDev. Roles usa exclusivamente AquaControlRoleTests y datos FICTICIOS. La revisión visual web utiliza una cuenta QA en esa misma BD de pruebas, no cuentas reales. Los archivos `.local`, contraseñas QA, backups privados y salidas APK permanecen fuera del seguimiento Git.

Pendiente de aceptación en dispositivo físico: verificar permisos GPS/segundo plano del fabricante, cámara, carga por red móvil y navegación giro a giro durante un recorrido real. No se dispone de un teléfono conectado por ADB en esta sesión. También quedan para entrega productiva firma release, hosting HTTPS, política de retención GPS y revisión de dependencias con avisos de vulnerabilidad. La APK debug sirve para pruebas locales.
