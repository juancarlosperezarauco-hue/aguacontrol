# AquaControl

Aplicación web de gestión de agua con ASP.NET Core, EF Core, SQL Server Spatial, Bootstrap y Leaflet. El código está separado en Domain, Application, Infrastructure, API y Web.

## Estado de esta entrega

Los módulos contienen persistencia real y API; no hay clientes reales inventados. La base `AquaControlDev` conserva el padrón comercial vacío para introducir datos autorizados. Los Shapefiles originales se importan con procedencia. Las pruebas usan exclusivamente `AquaControlTests` y nombres FICTICIO/TEST.

QR y tarjeta tienen un flujo de **sandbox**, claramente identificado, con confirmación restringida al personal autorizado. **No realiza cargos reales.** Para producción debe implementarse el adaptador del proveedor contratado, sus credenciales, firma de eventos y conciliación. No se solicitan ni almacenan números de tarjeta o CVV. La aplicación rechaza iniciar cobros si el proveedor no está configurado y el sandbox está deshabilitado.

## Ejecutar en este equipo

Para preparar una copia y enviarla a otro equipo, siga primero [GUIA_ENTREGA.md](GUIA_ENTREGA.md). Esta guía distingue el paquete de código y Shapefiles de un respaldo SQL Server, que debe transferirse por separado si se desea conservar la base ya poblada.

Desde la carpeta del proyecto original:

```powershell
& .\AquaControl\scripts\run.ps1 -SandboxPayments
```

Abrir `http://localhost:5080`. Las credenciales iniciales locales están en `AquaControl/.local/acceso-inicial.txt`, excluido del repositorio. No lo publique ni lo incluya en entregas compartidas.

El SQL Server local debe estar encendido. Las bases son distintas:

| Base | Uso |
|---|---|
| VisorDatosSIG | Original preservada; no es el destino de inicialización |
| AquaControlDev | Aplicación y cartografía de desarrollo |
| AquaControlTests | Pruebas automatizadas y datos ficticios |

## Instalar en otro equipo

1. Instalar .NET SDK 10 y SQL Server 2022 o compatible.
2. Configurar conexión mediante variable de entorno, sin guardar secretos en código:

```powershell
$env:ConnectionStrings__Aqua='Server=SU_SERVIDOR;Database=AquaControlDev;Integrated Security=true;Encrypt=true;TrustServerCertificate=false'
$env:AQUA_BOOTSTRAP_PASSWORD='REEMPLAZAR_POR_UN_SECRETO_PROPIO_LARGO'
& .\AquaControl\scripts\setup.ps1 -Initialize
& .\AquaControl\scripts\run.ps1 -SandboxPayments
```

El aprovisionamiento crea `aquadmin`; no use la contraseña ilustrativa de este documento. `setup.ps1` descarga recursos de Leaflet y Bootstrap desde sus distribuciones públicas y compila. La inicialización se destina únicamente a una base nueva con nombre AquaControl; no actualiza esquemas antiguos mediante EnsureCreated.

`database/01_AquaControl_Inicial.sql` contiene el DDL generado del modelo. Se puede ejecutar sobre una base vacía como alternativa a la creación por EF, y luego ejecutar `--init` para catálogos/administrador. Nunca ejecute el DDL inicial sobre una base con tablas existentes.

## Recorrido por módulos

1. **Usuarios y permisos:** crear cuentas, asignar un rol, desactivar, restablecer contraseña y vincular clientes al portal. Las contraseñas restablecidas requieren cambio. No es posible cambiar el propio rol desde la operación administrativa.
2. **Clientes y abonados:** crear cliente, cuenta y conexión; registrar dirección y coordenadas verificadas. Cada conexión exige un Código Fijo SIG único, por lo que el vínculo territorial queda trazable como Código Fijo → conexión → contrato → cliente. Vía y sector son referencias complementarias.
3. **Tarifas:** crear una versión con cargo fijo y precio por m³, o tramos contiguos desde cero hasta ilimitado. Una tarifa utilizada por un contrato no puede editarse; crear otra versión.
4. **Contratos:** asociar cliente, cuenta, conexión y tarifa. Solo hay un contrato abierto por cuenta y por conexión. Cerrar el anterior antes de cambiar titular. Las facturas anteriores siguen ligadas a su contrato original.
5. **Medidores:** crear medidor e instalarlo con lectura inicial. Para reemplazarlo, registrar lectura final del anterior. Se conserva el historial de instalaciones.
6. **Lecturas:** registrar período AAAA-MM y lectura no decreciente. Las estimaciones requieren motivo.
7. **Facturas:** emitir desde contrato y lectura. Se guardan conceptos, precios y datos del titular al emitir. Se impide repetir factura por contrato/período. Ajustes autorizados no alteran la factura original.
8. **Deudas:** saldo calculado desde total, ajustes, pagos aplicados y reversiones; no se mantiene una tabla independiente de deuda susceptible de divergencia.
9. **Pagos:** generar intento QR/tarjeta en sandbox, confirmar con perfil autorizado y consultar aplicaciones. La clave del intento y el evento externo impiden duplicados. El portal no ofrece reembolsos; las correcciones históricas se conservan en la bitácora interna.
10. **Tipos y actividades:** configurar tipos, efecto CORTE/RECONEXION/NINGUNO, actividades, orden y evidencia obligatoria. Las órdenes copian la definición para conservar su contenido histórico.
11. **Órdenes:** crear, asignar, reasignar, reprogramar y cancelar con motivos. Los operarios solo ejecutan asignaciones vigentes. Supervisor y ejecutor deben ser distintos.
12. **Ejecución:** EN_CAMINO → EN_EJECUCION, registrar actividades, materiales y archivos JPEG/PNG/PDF. Para finalizar deben cumplirse actividades y evidencias. Supervisor verifica y cierra.
13. **Avisos de corte:** primero crear política versionada de umbral/plazos y habilitarla. Emitir aviso de un contrato con deuda elegible; programar corte respetando el plazo.
14. **Pago y corte:** un pago confirmado reevalúa los avisos y cancela los cortes no ejecutados que pierdan causa, en la misma transacción que el pago. Notifica a supervisor y operario mediante notificaciones persistentes. Un pago parcial mantiene el corte si aún supera el umbral.
15. **SIG:** capas de manzanas, lotes, vías y códigos consultadas por extensión visible, con límite de 1.000 elementos por capa. Conexiones, órdenes y puntos de pago se superponen. Acerque el mapa si se alcanza el límite.
16. **Puntos de pago:** registrar entidad, banco/cooperativa/oficina/autorizado, dirección, horarios, métodos y coordenadas; aparecen en el mapa del personal autorizado.
17. **Reportes y auditoría:** resumen de órdenes, recaudación, materiales, exportación CSV de cartera y bitácora de operaciones.

El portal del cliente solo accede a clientes/contratos expresamente vinculados mediante UsuariosClientes. No se asignan clientes por semejanza de nombres SIG. El mapa general no está autorizado para el rol CLIENTE.

## Corte físico y concurrencia

El operario debe consultar y validar online antes de cortar. La autorización de corte dura 60 segundos y se invalida por pago. Debe registrar el efecto físico antes de finalizar. Un servicio ya cortado no se marca automáticamente como reconectado al pagar: el supervisor recibe aviso para evaluar la reconexión.

El sistema no puede hacer atómica una acción humana sobre una válvula con un pago bancario. Ante un pago posterior a la última validación debe detenerse el corte si todavía es físicamente posible. No se admiten cortes offline.

## Importar SIG

```powershell
& .\AquaControl\scripts\import-sig.ps1
```

Valida componentes, PRJ, UTF-8, correspondencia SHX/DBF y geometrías. Reintentar el mismo archivo no duplica; una versión distinta queda bloqueada hasta conciliación. No repara geometrías automáticamente. Preserva los atributos DBF en OriginalJson, la geometría original válida en SQL y todos los archivos fuente sin modificar. Las coordenadas de consulta proceden de la geometría.

La carga conserva cada capa en una transacción; una capa completada no se revierte si falla la siguiente. La asociación posterior se puede reintentar. Coincidencias espaciales múltiples no generan vínculos automáticos ni reemplazan los vínculos existentes. La regla inicial lote/manzana utiliza punto interior y requiere revisión catastral para casos de solape o límites complejos.

### Validación SIG y mapa

La instalación con los datos SIG originales se consulta en `http://localhost:5080/#map`, usando `AquaControlDev`. La base `AquaControlTests` del puerto 5081 contiene pruebas operativas y no representa la cartografía importada.

El mapa consulta las capas por extensión visible, muestra hasta 1.000 geometrías por capa y avisa cuando hay más resultados. Manzanas y vías aparecen en la vista general; lotes y códigos fijos se cargan desde el zoom 16 al activar sus casillas. “Ver territorio” ajusta la vista a la extensión de las capas. Las ventanas de detalle muestran el identificador y el vínculo con lote/manzana cuando existe. Las conexiones muestran el cliente accesible según los contratos cargados.

Para repetir la auditoría de solo lectura, después de compilar:

```powershell
& .\AquaControl\scripts\validate-sig.ps1
sqlcmd -S 'np:\\.\pipe\sql\query' -E -d AquaControlDev -b -i .\AquaControl\database\03_Verificar_SIG.sql
```

El informe completo queda en `output/analisis/Validacion_SIG.json`, con IDs, filas originales, coordenadas y candidatos de los vínculos pendientes; también enumera los lotes vinculados que no están totalmente cubiertos por su manzana. No aplica correcciones. Consulte `output/analisis/AquaControl_Validacion_SIG.md` para interpretar los resultados y las limitaciones catastrales.

Para probar las consultas de la API sin crear registros de negocio:

```powershell
python .\AquaControl\tests\sig_api.py http://127.0.0.1:5080 --password-file .\AquaControl\.local\acceso-inicial.txt --report .\output\analisis\Pruebas_SIG_API.json
```

## Migración y respaldo

- `00_Backup_Original.sql`: respaldo COPY_ONLY con checksum y RESTORE VERIFYONLY. VERIFYONLY no sustituye un ensayo completo de restauración.
- `02_Importar_Legado.sql`: preserva usuarios, roles y menús de la instalación inspeccionada en la base independiente. Contiene precondición de órdenes originales vacías. Se bloquea si hay órdenes reales pendientes de mapeo.
- Los tres usuarios heredados se copian con sus credenciales antiguas y quedan desactivados con cambio de contraseña requerido. Los roles LEGADO conservan su procedencia; el administrador debe asignar permisos operativos revisados.
- Los menús y autorizaciones originales se archivan íntegros como JSON en MigracionLegado. No se convierten automáticamente en privilegios de API.
- La base original sigue disponible para rollback de la instalación anterior. Después de registrar operaciones nuevas, no restaurar un respaldo antiguo sobre AquaControl: conciliar y aplicar una corrección que preserve pagos/trabajos nuevos.

## Pruebas

Ejecutar el servidor contra `AquaControlTests` y puerto 5081:

```powershell
$env:ConnectionStrings__Aqua='Server=lpc:localhost;Database=AquaControlTests;Integrated Security=true;Encrypt=false;TrustServerCertificate=true'
# Inicializar primero esta base vacía con --init y la contraseña de pruebas.
& .\AquaControl\scripts\run.ps1 -Port 5081 -SandboxPayments
```

En otra terminal:

```powershell
python .\AquaControl\tests\integration.py http://127.0.0.1:5081 --password-file .\AquaControl\.local\acceso-inicial.txt
```

El script no borra tablas: añade registros ficticios identificables por ejecución. Valida API, SQL real, permisos, consumo/facturación, idempotencia, pago parcial/completo, cancelación de corte, evidencias/materiales y verificación.

## Límites que deben resolverse antes de producción

- Adaptador real QR/tarjeta, webhooks firmados y conciliación externa, si el alcance cambia a cobros reales. El portal actual no ofrece reembolsos.
- Facturación fiscal según requisitos de la entidad; los documentos actuales son de cobro interno.
- La facturación inicial usa una lectura por factura; un reemplazo de medidor dentro del mismo período debe resolverse con facturación de múltiples lecturas antes de usar ese caso real.
- Versionado formal de migraciones incrementales, configuración de políticas reales y ensayo completo de restauración.
- Entrega de notificaciones por correo/SMS no incluida; el canal implementado es el portal persistente con consulta periódica.
- Listas operativas acotadas a 500 registros y selectores a 1.000; ampliar paginación/filtros para volúmenes comerciales superiores. Las exportaciones actuales corresponden a la lista consultada.
- Validación de capacidad/carga, antimalware de evidencias, política de retención, servidor HTTPS y cuenta SQL de mínimos privilegios antes de exposición pública.

La clave de conexión de desarrollo permite únicamente el servidor local por Windows. Producción exige configurar certificados y conexión explícita; no reutilizar `TrustServerCertificate=true` fuera de un entorno local controlado.
# Carga de datos reales

Las capas SIG ya están importadas. Para incorporar el padrón comercial real, copie los CSV recibidos a una carpeta de trabajo con la estructura de [templates/carga-comercial](templates/carga-comercial/README.md) y valide los encabezados antes de importar:

```powershell
.\scripts\validar-carga-comercial.ps1 -Ruta C:\ruta\a\carga-comercial
```

La validación no modifica SQL Server. La importación definitiva se ejecutará sobre una copia respaldada una vez que se disponga de la fuente oficial.
