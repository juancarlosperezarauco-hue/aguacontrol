# Manual de usuario — AquaControl

## 1. Propósito

AquaControl administra el servicio de agua potable desde una vista territorial. **Capas y búsqueda SIG** es la pantalla principal y reúne el mapa operativo y la consulta geográfica: cada punto representa una conexión de un abonado y permite conocer su estado, medidor, saldo, ubicación y trabajos asociados.

El sistema guarda los cambios en la base de datos y registra las acciones relevantes en la bitácora.

## 2. Ingreso al sistema

1. Abra la dirección entregada por la administración, por ejemplo `http://localhost:5080` en una instalación local.
2. Ingrese el usuario y contraseña asignados.
3. Si la cuenta tiene contraseña temporal, el sistema solicitará cambiarla antes de continuar.
4. Para salir, pulse **Salir** en la esquina superior derecha.

Cada perfil ve solamente las opciones autorizadas para su trabajo.

### Aplicación Android de campo

La APK utiliza el mismo usuario y contraseña. El operario entra a sus trabajos y puntos; el supervisor, administrador o dueño entra al mapa de monitoreo. Durante la jornada iniciada voluntariamente desde el teléfono se transmite GPS y la supervisión consulta actualizaciones cada 10 segundos. Al finalizar se detiene el envío. Una posición antigua se identifica como sin señal, no como ubicación en vivo.

Para finalizar un trabajo se debe subir una fotografía del servicio y del medidor actual, además de completar las actividades y los efectos físicos que correspondan. La serie se conserva en la evidencia para su revisión desde el detalle de la orden. Consulte la [guía de Android](../mobile/android/README.md) para instalar, configurar la dirección del servidor y probar cámara/GPS.

## 3. Pantalla principal: Capas y búsqueda SIG

Al ingresar se abre **Capas y búsqueda SIG**. Esta pantalla concentra la operación diaria, las rutas de trabajo, la consulta de atributos y las capas del territorio. La entrada independiente «Mapa operativo» fue unificada aquí; sus enlaces anteriores también abren esta pantalla.

Los buscadores de servicios y territorio están encima del mapa, con los campos y botones organizados verticalmente. Debajo del mapa se encuentran el detalle del servicio seleccionado, las capas, los filtros y las herramientas de rutas. Las acciones de lecturas, órdenes y avisos dependen de los permisos del usuario.

### Inicio optimizado

El mapa inicia sin puntos operativos ni capas SIG cargadas. Esto evita descargar el padrón completo y mantiene una navegación fluida. Primero busque el servicio requerido; el mapa mostrará solamente los resultados encontrados.

### Qué puede mostrar

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

1. En **Buscar servicio**, escriba al menos dos caracteres del titular, Código Fijo, abonado o conexión.
2. Pulse **Buscar en mapa**.
3. Si existe un único resultado, el mapa se centra automáticamente. Si hay varios, pulse **Ver** sobre el servicio requerido.
4. Pulse el punto de usuario o medidor.
5. En **Servicio seleccionado** aparecerán el cliente, abonado, código de conexión, medidor, saldo y dirección.
6. Use los botones de la ficha según los permisos de su perfil:
   - **Crear orden**: abre una orden ya vinculada a la conexión y coordenadas del punto.
   - **Registrar lectura**: registra la lectura del medidor seleccionado.
   - **Emitir aviso**: genera un aviso de corte para el contrato activo.
   - **Abrir clientes y servicios**: consulta el padrón completo.

## 4. Filtros y búsqueda territorial

En el panel izquierdo del mapa puede:

- Activar o desactivar las capas de manzanas, lotes, vías y códigos fijos.
- Filtrar los resultados encontrados por **estado de servicio** y condición de medidor.
- Buscar un código fijo, lote, manzana o vía mediante el cuadro **Buscar en territorio**.
- Usar **Ver territorio** para regresar a la extensión general de las capas.
- Cambiar la cartografía base con el selector del mapa: **sin fondo**, vista clara, vista oscura, satélite o topográfico.

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

1. El **Administrador** crea la cuenta y le asigna **OPERARIO LECTURAS Y FACTURACION** o **OPERARIO TECNICO**, según su función, desde **Configuración avanzada → Usuarios y permisos**.
2. El supervisor crea las órdenes de lectura, instalación, mantenimiento, reparación o corte para los puntos del mapa.
3. En cada orden, pulse **Asignar** y elija el operario responsable.
4. Regrese a **Capas y búsqueda SIG** y, en **Ruta de trabajo**, seleccione el operario y, si corresponde, la fecha programada.
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
| Super administrador | Dueño del sistema, control total y administración de otros dueños. |
| Administrador | Acceso general, creación de usuarios y roles administrativos adicionales. No modifica la cuenta del dueño. |
| Supervisor | Órdenes, avisos de corte, asignaciones, programación y verificación. |
| Operario de lecturas y facturación | Registra consumos y emite recibos de sus lecturas en trabajos asignados en ejecución. |
| Operario técnico | Instala, corta, reconecta y mantiene servicios; registra actividades, evidencias y materiales. |

Los cinco roles base son fijos. El administrador puede crear perfiles administrativos adicionales. Los roles genéricos anteriores y los heredados están archivados. Consulte [Jerarquía de roles](12_Jerarquia_Roles.md) para la actualización y recuperación de permisos.

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

