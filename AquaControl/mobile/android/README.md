# AquaControl Móvil 0.4 · Android

Aplicación nativa para operarios y monitoreo de supervisión. Comparte la base y la API del sistema web; no lleva una copia de clientes ficticios.

## Ingreso por rol

La dirección del servidor se introduce en el login. La aplicación recuerda únicamente esa dirección; las cookies de sesión permanecen en memoria. El primer ingreso con una contraseña temporal obliga a cambiarla.

- **Operario de lecturas y facturación:** sus órdenes, mapa de puntos, navegación, registro de consumo y emisión de recibos.
- **Operario técnico:** sus instalaciones, cambios, cortes, reconexiones y mantenimientos. Puede registrar el medidor instalado en la orden.
- **Supervisor / administrador / dueño:** mapa y lista de seguimiento. No transmiten GPS como operarios. La planificación, usuarios y asignaciones siguen en la web.
- Un perfil adicional requiere `field.monitor` para acceder al monitoreo.

## Uso del operario

1. Ingresar con la cuenta creada por el administrador.
2. Seleccionar **Iniciar jornada y compartir ubicación**. Leer el aviso y autorizar la ubicación. En Android 13+ se solicita también permiso para notificaciones.
3. Ver los puntos en el orden guardado por el supervisor. La línea azul sigue las vías SIG y sus paradas están numeradas; los trabajos sin ruta se identifican aparte. **Navegar** abre indicaciones giro a giro en Google Maps, a pie, en moto (según cobertura) o vehículo.
4. Abrir el trabajo, iniciar traslado y comenzar ejecución.
5. Registrar lectura/recibo o instalación, según el perfil y tipo de orden.
6. Completar actividades y subir una fotografía del servicio y del medidor. Guardar una visita con GPS: REALIZADO exige foto, número observado y lectura del medidor actual; OBSERVADO exige un motivo, por ejemplo casa cerrada. El servidor verifica frescura, precisión y cercanía del GPS al destino.
7. Para corte/reconexión, confirmar el efecto físico realizado después de registrar la visita. Finalizar cuando corresponda; NO_REALIZADA exige una visita OBSERVADO con GPS y no admite un efecto físico ya registrado. El supervisor verifica y cierra después.
8. **Finalizar jornada** detiene el GPS y cierra la jornada. También puede detenerse desde la notificación de Android. Cerrar sesión detiene el seguimiento.

Las fotos se comprimen en el dispositivo y se guardan mediante la API de evidencias. La relación con orden, conexión y serie del medidor la determina el servidor. Las fotos antiguas no se atribuyen automáticamente al equipo actual.

Los cortes siempre requieren conexión, aviso vigente y validación online de 60 segundos. La foto no sustituye la autorización ni el registro del efecto físico.

## Seguimiento

El teléfono intenta enviar GPS cada 10 segundos durante una jornada iniciada por el usuario. Usa un servicio en primer plano con notificación visible, iniciado desde la app abierta. No solicita ubicación permanente ni arranca al encender el teléfono.

La web y la APK de supervisión consultan posiciones cada 10 segundos. Señal de más de 90 segundos se etiqueta **SIN SEÑAL**; es la última posición conocida, no ubicación en vivo. Al cerrar la jornada se dejan de mostrar las coordenadas. Precisión, señal, conectividad, restricciones de batería y disponibilidad del dispositivo pueden limitar la actualización.

No se inventan posiciones. Como se limpiaron las cuentas de prueba, el administrador debe crear nuevos operarios y un supervisor antes de probar un recorrido. No existe envío de trabajos por push en esta versión: las órdenes se consultan cada 20 segundos con la pantalla de trabajos abierta.

## Preparar backend y base

Con servidor detenido, respaldar la base y ejecutar desde AquaControl:

```powershell
.\scripts\upgrade-scope.ps1 -SqlServer 'localhost' -Database 'AquaControlDev'
.\scripts\run-mobile.ps1
```

El script verifica un respaldo y aplica los incrementos 15 y 16: jornadas, evidencias, rutas, paradas, visitas e historial GPS. No borra datos. Si todavía utiliza los roles anteriores, ejecute primero `scripts/upgrade-roles.ps1`; esa actualización incorpora los permisos de campo y obliga a volver a ingresar. Usuarios archivados permanecen archivados. Consulte [Alcance y flujo completo](../../docs/13_Alcance_SIG_2_2026.md).

## Compilar e instalar

Requisitos: Android Studio, JDK 21, Android SDK 35, Build Tools 35.0.0 y Gradle 8.9 (AGP 8.7.3).

Abrir esta carpeta en Android Studio y ejecutar **Build → Build APK(s)**. Si está disponible el wrapper:

```powershell
.\gradlew.bat assembleDebug
```

APK de prueba: `app/build/outputs/apk/debug/app-debug.apk`.

La APK se genera localmente y está excluida de Git. Verifique su huella con `Get-FileHash app/build/outputs/apk/debug/app-debug.apk -Algorithm SHA256`. La versión es 0.4.0 (código 4). Las pruebas de cámara y GPS requieren un teléfono físico.

- Emulador Android: servidor `http://10.0.2.2:5080`.
- Teléfono en la misma red: ejecutar el backend con `scripts/run-mobile.ps1` (equivalente a `scripts/run.ps1 -Host lan`) y usar `http://IP_DEL_SERVIDOR:5080`. El script muestra la dirección de la interfaz con puerta de enlace, evitando confundirla con IPs de VMware o VirtualBox. El firewall debe permitir el ejecutable de AquaControl en el perfil de red usado.
- En este equipo se conserva `lan` en `.local/listen-host.txt`, por lo que el arranque habitual también admite el celular. Este archivo es local y no se publica en Git; otros equipos deben usar el inicio móvil explícito. Para volver a acceso solo local, ejecutar `scripts/run.ps1 -Host localhost`.
- Equipos en redes distintas: usar un servidor HTTPS accesible. `localhost` en el teléfono apunta al propio teléfono, no a la computadora.
- La versión **debug** admite HTTP para pruebas. La versión **release** exige HTTPS y requiere una clave de firma propia; no se proporciona una firma de producción.

Transferir el APK por un canal privado e instalarlo en un Android 8.0 o superior. El servidor debe permanecer encendido y ser accesible desde el teléfono.

## Comprobaciones

Las pruebas SQL del proyecto `tests/Roles` incluyen aislamiento por operario, jornadas, GPS inválido/antiguo, suplantación, fin de jornada y fotografía obligatoria. Las pruebas de servicios no sustituyen una prueba física de permisos, GPS, cámara, red y consumo de batería en los dispositivos que se usarán.

## Entrega local firmada

`./scripts/build-mobile-local.ps1` desde AquaControl genera `output/mobile/AquaControl-Local-v0.4.apk`. La variante local (`bo.aquacontrol.movil.local`) se instala aparte de debug, está firmada con una clave privada del equipo, no es depurable y permite HTTP solo a IPs privadas/locales. Para destinos de Internet exige HTTPS. Los secretos de firma se guardan en `.local/signing`, excluido de Git y del paquete. Conservarlos para futuras actualizaciones. Consulte [Entrega local](../../docs/14_Entrega_Local.md).
