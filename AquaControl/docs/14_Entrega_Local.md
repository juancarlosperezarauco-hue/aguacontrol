# AquaControl 0.4 — entrega para equipo local

El destino acordado es una computadora Windows con SQL Server y teléfonos conectados a la misma red. El código está organizado por capas; no se requiere contratar hosting para esta entrega.

## Qué incorpora

- Selección de servicios por cliente, Código Fijo o medidor, desde el mapa de asignaciones.
- Creación conjunta de órdenes, actividades, asignaciones y recorrido vial al guardar. La vista previa no crea órdenes. Reintentar la misma solicitud no duplica registros.
- Cortes con aviso vigente y deuda habilitada; reconexiones de servicios cortados; lecturas de medidores instalados en servicios activos. Se excluyen instalaciones pendientes y trabajos abiertos incompatibles.
- Reproducción, pausa, reinicio, velocidad y exportación CSV del historial GPS. Se reproducen posiciones recibidas, sin inventar ubicaciones entre envíos.
- Visor 3D con calles, satélite, topográfico o sin fondo, capas, inclinación, vista superior, desplazamiento y selección de servicios encontrados. La altura es esquemática.
- APK firmada **AquaControl Local**, con HTTP restringido a direcciones privadas/locales y HTTPS disponible. Se instala aparte de la app debug anterior.
- Dependencia `System.Security.Cryptography.Pkcs` actualizada a 9.0.21. Referencias: [aviso de Microsoft CVE-2026-84008](https://github.com/dotnet/announcements/issues/468) y [CVE-2026-70162](https://github.com/dotnet/announcements/issues/454).

## Ejecutar en este equipo

Desde la carpeta **AquaControl**:

```powershell
.\scripts\start-local.ps1 -Restart
```

El script compila si ejecuta el código fuente, comprueba la BD e inicia el servidor en segundo plano. Muestra la dirección web y la dirección IP para el teléfono. No modifica el firewall ni elimina datos. Los registros quedan en `.local/logs`.

Abrir **http://localhost:5080**. Ingresar con una cuenta existente creada por el administrador. No se incluyen contraseñas en el paquete ni se crean usuarios reales automáticamente.

Si usa otra instancia SQL Server:

```powershell
.\scripts\start-local.ps1 -SqlServer '.\SQLEXPRESS' -Database 'AquaControlDev' -Restart
```

Si ya existe `ConnectionStrings__Aqua`, esa configuración tiene prioridad sobre los parámetros de conexión del script. La computadora debe permanecer encendida durante el uso desde teléfonos.

## Actualizar una BD existente

Con AquaControl detenido y usando una cuenta SQL autorizada para respaldo y cambios de esquema:

```powershell
.\scripts\upgrade-scope.ps1 -SqlServer 'localhost' -Database 'AquaControlDev'
```

Comprueba un respaldo y aplica los incrementos 15, 16 y 17. No ejecuta el script 14 de limpieza de cuentas; ese archivo corresponde a una operación histórica específica y no forma parte de la instalación ni de la actualización habitual.

El incremento 17 agrega `RequestId` y `SourceHash` a RutasCampo e índice único para evitar duplicados ante reintentos de red. Conserva las rutas, paradas, clientes, fotos y geometrías anteriores. Para volver al binario anterior se conservan esas columnas; no hace falta eliminar datos.

## Instalar la APK en el teléfono

1. Copiar **AquaControl-Local-v0.4.apk** desde `output/mobile` o `instalar-android` del paquete.
2. Instalarla en Android 8 o posterior. La app se llama **AquaControl Local** y puede convivir con la anterior versión de pruebas.
3. Conectar teléfono y computadora a la misma red.
4. Introducir la dirección que muestra el iniciador, por ejemplo `http://192.168.1.5:5080`. No usar localhost en el teléfono. La IP puede cambiar.
5. Ingresar con la cuenta del operario o supervisor creada desde la web.
6. Iniciar jornada y autorizar ubicación precisa y notificaciones. El operario debe esperar un envío GPS antes de guardar una visita.

Para Internet se requiere HTTPS. Esta entrega local no publica el servidor en Internet ni abre puertos del router. Mapas base y navegación externa necesitan conectividad a sus proveedores.

El administrador conserva privadamente `.local/signing/aquacontrol-local.p12` y `.local/signing/local-signing.json`. Ambos son necesarios para firmar futuras actualizaciones compatibles; no se envían con el instalador, no se suben a Git y no deben perderse. La APK no contiene esa clave privada.

## Instalación limpia en otro equipo

Requisitos: SQL Server 2022 o compatible, .NET SDK 10 para compilar/inicializar y `sqlcmd` para los scripts de respaldo/actualización. El servidor publicado necesita el runtime ASP.NET Core 10; instalar el SDK también lo proporciona.

Puede restaurar un respaldo privado para continuar con los mismos datos y usuarios. Si usa una BD vacía, definir una contraseña inicial propia y ejecutar:

```powershell
$env:ConnectionStrings__Aqua='Server=localhost;Database=AquaControlDev;Integrated Security=true;Encrypt=false;TrustServerCertificate=true'
$env:AQUA_BOOTSTRAP_PASSWORD='REEMPLAZAR_POR_UNA_CONTRASENA_PROPIA_DE_12_O_MAS_CARACTERES'
.\scripts\setup.ps1 -Initialize
Remove-Item Env:AQUA_BOOTSTRAP_PASSWORD
.\scripts\start-local.ps1
```

Esto crea el modelo completo. Una BD nueva no contiene clientes, medidores ni tarifas reales: deben importarse/registrarse. Los datos SIG incluidos se importan desde la pantalla de importación seleccionando sus componentes originales. El migrador no reemplaza el padrón comercial ni inventa series de medidor o tarifas oficiales.

## Respaldos e historial

```powershell
.\scripts\backup-local.ps1 -SqlServer 'localhost' -Database 'AquaControlDev'
```

SQL Server guarda el `.bak` en su carpeta predeterminada y verifica su integridad. El archivo contiene información privada y se comparte por un canal privado, separado del código.

Las fotos son archivos externos a SQL: respaldar también `Storage__EvidencePath` o, en el proyecto fuente, `src/AquaControl.API/App_Data/evidence`. En el paquete publicado, el iniciador usa `.local/evidence`. Al restaurar una BD con fotos, copiar allí los archivos anteriores o configurar la ruta original; de lo contrario no podrán descargarse.

Por defecto se conserva todo el historial GPS y de visitas, sin borrado automático. Los perfiles con `field.monitor` pueden exportarlo desde Monitoreo. Los archivos exportados contienen ubicaciones y deben mantenerse privados. Definir una política de eliminación requiere una decisión del responsable; no se impone un plazo ni se borran datos arbitrariamente.

## Generar otra entrega

```powershell
# JDK 21 y Android SDK 35 configurados
.\scripts\build-mobile-local.ps1
.\scripts\package-local.ps1
```

La entrega ZIP incluye código, servidor publicado, web, scripts SQL, datos SIG versionados, guía, APK local firmada y un manifiesto con huellas SHA-256. Excluye respaldos SQL, contraseñas, claves privadas, fotos y archivos temporales. El código de firma release para un futuro despliegue HTTPS también está preparado mediante variables de entorno.

## Prueba de aceptación del teléfono

La compilación y la firma se verifican en el equipo de desarrollo. Todavía se debe recorrer con un teléfono físico: login, permisos de ubicación/notificaciones, señal en segundo plano, navegación, cámara, envío de fotografía, visita con GPS, resultado y cierre por el supervisor. No hay un dispositivo conectado por ADB en esta sesión; las pruebas automatizadas no sustituyen esta comprobación de hardware y red.

OCR del número del medidor continúa siendo opcional; se registra manualmente.
