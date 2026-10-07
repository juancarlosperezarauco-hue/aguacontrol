# Manual de usuario — AquaControl

## 1. Propósito

AquaControl administra el servicio de agua potable desde una vista territorial. El **Mapa operativo** es la pantalla principal: cada punto representa una conexión de un abonado y permite conocer su estado, medidor, saldo, ubicación y trabajos asociados.

El sistema guarda los cambios en la base de datos y registra las acciones relevantes en la bitácora.

## 2. Ingreso al sistema

1. Abra la dirección entregada por la administración, por ejemplo `http://localhost:5080` en una instalación local.
2. Ingrese el usuario y contraseña asignados.
3. Si la cuenta tiene contraseña temporal, el sistema solicitará cambiarla antes de continuar.
4. Para salir, pulse **Salir** en la esquina superior derecha.

Cada perfil ve solamente las opciones autorizadas para su trabajo.

## 3. Pantalla principal: Mapa operativo

Al ingresar se abre el **Mapa operativo**. Esta pantalla concentra la operación diaria.

### Qué muestra

- Límites y capas SIG: manzanas, lotes, vías y códigos fijos.
- Usuarios, conexiones y medidores georreferenciados.
- Órdenes de trabajo ubicadas en el territorio.
- Referencias cercanas, como bancos, centros de salud y oficinas.
- Una leyenda que se actualiza con los filtros aplicados.

### Significado de colores de los usuarios

| Color | Estado | Significado |
|---|---|---|
| Rojo | Servicio cortado | La conexión tiene el suministro interrumpido. |
| Naranja | Pendiente de corte | Tiene aviso vigente o gestión de corte pendiente. |
| Amarillo | Con saldo pendiente | Tiene una factura o saldo pendiente de regularización. |
| Verde | Al día | Servicio activo sin saldo pendiente identificado. |
| Azul | Instalación pendiente | Aún falta instalar el medidor o completar la conexión. |

### Consultar un usuario desde el mapa

1. Acerque el mapa si los puntos están agrupados.
2. Pulse un punto de usuario o medidor.
3. En **Servicio seleccionado** aparecerán el cliente, abonado, código de conexión, medidor, saldo y dirección.
4. Use los botones de la ficha según los permisos de su perfil:
   - **Crear orden**: abre una orden ya vinculada a la conexión y coordenadas del punto.
   - **Registrar lectura**: registra la lectura del medidor seleccionado.
   - **Emitir aviso**: genera un aviso de corte para el contrato activo.
   - **Abrir clientes y servicios**: consulta el padrón completo.

## 4. Filtros y búsqueda territorial

En el panel izquierdo del mapa puede:

- Activar o desactivar las capas de manzanas, lotes, vías y códigos fijos.
- Filtrar los usuarios por **estado de servicio**, condición de medidor y cliente/abonado.
- Buscar un código fijo, lote, manzana o vía mediante el cuadro **Buscar en territorio**.
- Usar **Ver territorio** para regresar a la extensión general de las capas.
- Cambiar la cartografía base con el selector del mapa: calles, vista clara, vista oscura, satélite o topográfico.

Los lotes y códigos fijos se muestran al acercar el zoom para evitar sobrecargar la pantalla.

## 5. Registrar clientes y servicios

Los perfiles con permiso de administración pueden pulsar **Nuevo cliente** desde el mapa. Complete los datos personales y de contacto solicitados. Luego, en **Configuración avanzada**, complete los registros técnicos relacionados:

1. Cree el **abonado** o cuenta del servicio.
2. Registre la **conexión**, incluyendo código, dirección, latitud, longitud y estado.
3. Cree el **contrato** que vincula cliente, abonado y conexión.
4. Registre el **medidor** y su instalación en la conexión.

Una conexión necesita coordenadas válidas para aparecer como punto en el mapa. Al guardar los datos, regrese al Mapa operativo y actualice la vista.

## 6. Lecturas, consumo y facturas

### Registrar una lectura

Hay dos formas:

- Desde el punto seleccionado en el Mapa operativo, pulse **Registrar lectura**.
- Entre a **Lecturas y consumo** y pulse **Registrar lectura**.

Seleccione la instalación, indique el período en formato `AAAA-MM`, la lectura actual y, si corresponde, marque que es una estimación. El sistema conserva la lectura anterior para calcular el consumo.

### Ruta masiva de lectura y recibos

Para preparar la lectura mensual, el supervisor o administrador abre **Lecturas y consumo → Planificar ruta de lecturas**. El sistema selecciona automáticamente los medidores que cumplen estas condiciones:

- Tienen instalación de medidor activa y medidor activo.
- Pertenecen a un contrato vigente.
- La conexión no está cortada, inactiva ni pendiente de instalación.
- Aún no tienen lectura ni una orden abierta para el período elegido.

Seleccione el período, supervisor, operario y fecha programada. AquaControl crea una orden de tipo **Lectura de medidor** por cada medidor elegible, las asigna al operario y las incorpora a su ruta en el mapa.

El operario abre cada parada de su ruta y pulsa **Registrar lectura**. Una vez cargadas las lecturas, el administrador pulsa **Generar recibos del período**. El sistema calcula automáticamente el consumo usando `lectura actual − lectura anterior`, aplica la tarifa vigente y genera los recibos o facturas pendientes del período.

### Emitir una factura

1. Abra **Facturas y saldos**.
2. Pulse **Emitir factura**.
3. Seleccione el contrato, la lectura del período y la fecha de vencimiento.
4. Revise el total, saldo y estado de la factura.

Este proyecto opera los saldos y avisos. Los pagos son simulados o administrativos; no procesa tarjetas, QR ni cobros bancarios reales.

## 7. Órdenes de trabajo

### Crear una orden desde el mapa

1. Seleccione el punto del servicio.
2. Pulse **Crear orden**.
3. Seleccione tipo de trabajo, prioridad, fecha programada y supervisor.
4. Verifique que la conexión, contrato y coordenadas correspondan al punto elegido.
5. Agregue instrucciones y guarde.

También se puede crear una orden general desde **Órdenes de trabajo**.

### Planificar la ruta del operario

1. El **Administrador** crea la cuenta del operario y le asigna el rol **OPERARIO** desde **Configuración avanzada → Usuarios y permisos**.
2. El supervisor crea las órdenes de lectura, instalación, mantenimiento, reparación o corte para los puntos del mapa.
3. En cada orden, pulse **Asignar** y elija el operario responsable.
4. Regrese al **Mapa operativo** y, en **Ruta de trabajo**, seleccione el operario y, si corresponde, la fecha programada.
5. Pulse **Trazar ruta**. El sistema dibuja puntos numerados y una línea de recorrido sugerida por cercanía entre las órdenes asignadas.
6. Pulse el número de una parada para abrir la orden y registrar la visita, actividades, lectura, materiales, evidencias y estado de ejecución.

La ruta muestra el orden operativo de las paradas y no reemplaza la decisión del supervisor ni las condiciones reales de tránsito. El operario puede usar las coordenadas del punto para navegar hasta el destino.

### Asignar y ejecutar

El supervisor abre la orden y puede asignar un operario, reprogramar y controlar el avance. El operario ve sus trabajos asignados, registra las actividades realizadas, materiales, evidencias y observaciones.

Los estados siguen esta secuencia:

`PENDIENTE → ASIGNADA → EN_CAMINO → EN_EJECUCION → FINALIZADA → VERIFICADA → CERRADA`

También existen los estados **REPROGRAMADA**, **NO_REALIZADA** y **CANCELADA**. Una orden cancelada no puede volver a ejecución.

### Corte y reconexión

Para trabajos con efecto de corte o reconexión, el operario debe registrar el efecto físico solamente después de ejecutar la acción en campo. Antes de un corte, la orden permite validar la deuda vigente; si el aviso fue resuelto, no debe continuarse con el corte.

## 8. Avisos de corte

1. Seleccione un servicio con saldo pendiente en el mapa.
2. Pulse **Emitir aviso**, o use el módulo **Avisos de corte**.
3. El sistema aplica la política de corte configurada y programa la fecha.
4. Desde la lista de avisos, el supervisor puede resolver el aviso cuando corresponda.

Al resolver un aviso, registre siempre el motivo administrativo. La orden de corte pendiente debe quedar cancelada o actualizada según el caso.

## 9. Módulos del menú

| Módulo | Uso principal |
|---|---|
| Mapa operativo | Operar servicios y trabajos desde la ubicación geográfica. |
| Clientes y servicios | Consultar clientes, abonados, conexiones y medidores. |
| Lecturas y consumo | Registrar lecturas y revisar el consumo calculado. |
| Facturas y saldos | Emitir facturas y consultar deudas. |
| Órdenes de trabajo | Crear, asignar, ejecutar y verificar trabajos. |
| Avisos de corte | Gestionar avisos y fechas de corte. |
| Capas y búsqueda SIG | Consultar las capas geográficas y sus atributos originales. |
| Configuración avanzada | Administrar catálogos, usuarios, roles, tarifas, actividades, materiales, auditoría e importaciones SIG. |

## 10. Perfiles de usuario

| Perfil | Responsabilidad habitual |
|---|---|
| Super administrador | Usuarios, roles, permisos, configuración y auditoría global. |
| Administrador | Crea las cuentas de administradores, supervisores, operarios y clientes; asigna roles y administra los datos comerciales. |
| Supervisor | Órdenes, avisos de corte, asignaciones, programación y verificación. |
| Operario | Trabajos asignados, actividades, evidencias, materiales y actualización de estado. |
| Cliente | Consulta de su propia información, servicio, deuda, facturas y avisos. |

Las opciones visibles pueden variar por los permisos concretos asignados a la cuenta.

## 11. Recomendaciones de operación

- Verifique la ubicación del punto antes de crear una orden.
- Registre coordenadas en formato decimal WGS84: latitud y longitud.
- No use datos ficticios como información definitiva de clientes.
- Adjunte evidencias solo después de realizar el trabajo en campo.
- No cambie manualmente una orden a cerrada si aún no fue verificada.
- Revise periódicamente la bitácora y los avisos vigentes.
- Mantenga resguardada la contraseña; cada usuario debe usar su propia cuenta.

## 12. Solución rápida de problemas

| Situación | Acción recomendada |
|---|---|
| No aparece un punto en el mapa | Revise que la conexión tenga latitud y longitud válidas. |
| No se ven lotes o códigos fijos | Acerque el zoom hasta nivel 16 o mayor y active la capa. |
| No puede guardar una acción | Revise que su rol tenga permiso y que complete los campos obligatorios. |
| No puede registrar lectura | Confirme que la conexión tenga una instalación de medidor activa. |
| No puede crear aviso | Verifique que existe un contrato activo para el servicio. |
| El mapa no carga | Recargue la página y compruebe la conexión al servidor. |

