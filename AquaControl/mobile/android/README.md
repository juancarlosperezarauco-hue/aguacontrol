# AquaControl Móvil para Android

Aplicación nativa enfocada inicialmente en el rol **OPERARIO**. Usa la misma API de AquaControl: inicia sesión, lista órdenes asignadas, muestra las actividades y permite registrar el avance del trabajo desde campo.

## Funciones incluidas

- Inicio de sesión contra `/api/auth/login` con cookie y protección CSRF.
- Consulta de órdenes asignadas al usuario autenticado.
- Detalle operativo: prioridad, fecha, instrucciones y ubicación.
- Apertura de la ubicación en la aplicación de mapas disponible en el teléfono.
- Transiciones `ASIGNADA → EN_CAMINO → EN_EJECUCION → FINALIZADA` sujetas a las reglas del servidor.
- Registro de actividades de la orden desde la aplicación.

## Abrir y generar APK

1. Instale Android Studio con Android SDK 35 y JDK 21.
2. Abra esta carpeta (`mobile/android`) como proyecto Gradle.
3. Espere a que Android Studio descargue Gradle y dependencias.
4. Para emulador use `http://10.0.2.2:5080` como URL de servidor.
5. Para un teléfono físico use la URL HTTPS pública de AquaControl. Durante una prueba local puede usar la IP LAN del servidor, por ejemplo `http://192.168.1.20:5080`.
6. Ejecute **Build > Build APK(s)**. El archivo se generará en `app/build/outputs/apk/debug/`.

`usesCleartextTraffic` está activado solo para facilitar pruebas locales por HTTP. Antes de distribuir la aplicación se debe publicar AquaControl con HTTPS y desactivarlo.

Para una URL pública, agregue el dominio del servidor en `AllowedHosts` de la configuración de la API. La configuración de desarrollo ya admite `localhost`, `127.0.0.1` y `10.0.2.2` para el emulador Android.
