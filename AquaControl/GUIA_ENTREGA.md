# Entrega de AquaControl en una carpeta comprimida

## Qué debes comprimir

Desde la carpeta base:

```text
C:\Users\HP\OneDrive\Desktop\Especificaciones_Proyecto_VisorDatosSIG_2026
```

Incluye estas dos carpetas:

```text
AquaControl\
DatosSIG_Reproj\
```

No es necesario incluir `.tools`, `src\**\bin`, `src\**\obj` ni los informes temporales de pruebas. `scripts\setup.ps1` recompila el proyecto y conserva los recursos web que ya estén en `Web\vendor`.

No envíes `AquaControl\.local\acceso-inicial.txt` por un canal público: contiene la contraseña del administrador de desarrollo. En el equipo receptor se debe crear una contraseña nueva durante la instalación.

## Requisitos del equipo receptor

Instalar:

1. Windows PowerShell 5 o PowerShell 7.
2. .NET SDK 10.
3. SQL Server 2022 o compatible con el servicio iniciado.
4. Permiso para crear una base de datos y usar autenticación integrada de Windows.

Si el equipo no tiene conexión a Internet, conserva `AquaControl\Web\vendor`; de ese modo Leaflet, QR y los estilos ya están incluidos. La primera restauración de paquetes de .NET sí puede requerir conexión si el caché de NuGet no existe.

## Instalación limpia con cartografía SIG

Abrir PowerShell en la carpeta base descomprimida. La base nueva debe usar un nombre que contenga `AquaControl`; el programa bloquea la inicialización accidental de `VisorDatosSIG`.

```powershell
$env:ConnectionStrings__Aqua='Server=np:\\.\pipe\sql\query;Database=AquaControlDev;Integrated Security=true;Encrypt=false;TrustServerCertificate=true'
$env:AQUA_BOOTSTRAP_PASSWORD='Escribe-aqui-una-clave-larga-y-nueva'

& .\AquaControl\scripts\setup.ps1 -Initialize
& .\AquaControl\scripts\import-sig.ps1
& .\AquaControl\scripts\run.ps1 -SandboxPayments
```

Si la instancia SQL no usa Named Pipes, reemplaza `Server=np:\\.\pipe\sql\query` por el servidor correspondiente, por ejemplo `Server=localhost` o `Server=SERVIDOR\\INSTANCIA`.

Abrir:

```text
http://localhost:5080
```

El usuario inicial será `aquadmin` y la contraseña será exactamente el valor de `AQUA_BOOTSTRAP_PASSWORD`. Después del primer acceso, cambia la contraseña desde «Mi contraseña» y guarda las credenciales fuera de la carpeta del proyecto.

## Usar una copia de la base ya preparada

La carpeta no incluye automáticamente `AquaControlDev`, porque es una base administrada por SQL Server. Si quieres enviar la cartografía y los datos comerciales actuales, crea un respaldo en el equipo de origen:

```sql
BACKUP DATABASE [AquaControlDev]
TO DISK = N'C:\\RutaSegura\\AquaControlDev_Entrega.bak'
WITH COPY_ONLY, CHECKSUM, INIT, COMPRESSION;
RESTORE VERIFYONLY FROM DISK = N'C:\\RutaSegura\\AquaControlDev_Entrega.bak' WITH CHECKSUM;
```

Entrega el `.bak` por un canal seguro y restáuralo en el SQL Server receptor. En ese caso no ejecutes `setup.ps1 -Initialize` sobre esa base restaurada; primero configura la conexión y ejecuta solamente:

```powershell
$env:ConnectionStrings__Aqua='Server=np:\\.\pipe\sql\query;Database=AquaControlDev;Integrated Security=true;Encrypt=false;TrustServerCertificate=true'
& .\AquaControl\scripts\run.ps1 -SandboxPayments
```

El respaldo contiene cuentas y datos operativos del entorno de origen. No lo publiques ni lo adjuntes a un repositorio.

## Comprobar la instalación

Para revisar la cartografía y sus índices:

```powershell
& .\AquaControl\scripts\validate-sig.ps1
sqlcmd -S 'np:\\.\pipe\sql\query' -E -d AquaControlDev -b -i .\AquaControl\database\03_Verificar_SIG.sql
```

La aplicación queda en `http://localhost:5080/#map`. Los pagos QR son simulados y la pantalla de tarjeta es demostrativa; no se procesan cargos reales.

## Entorno de pruebas separado

No uses `AquaControlTests` para operación. Para repetir las pruebas, inicializa una base nueva con nombre que contenga `AquaControl` y usa el puerto 5081:

```powershell
$env:ConnectionStrings__Aqua='Server=np:\\.\pipe\sql\query;Database=AquaControlTests;Integrated Security=true;Encrypt=false;TrustServerCertificate=true'
$env:AQUA_BOOTSTRAP_PASSWORD='Otra-clave-larga-solo-para-pruebas'
& .\AquaControl\scripts\setup.ps1 -Initialize
& .\AquaControl\scripts\run.ps1 -Port 5081 -SandboxPayments
python .\AquaControl\tests\integration.py http://127.0.0.1:5081 --password-file .\AquaControl\.local\acceso-inicial.txt
```

El script de integración crea registros ficticios identificados como `FICTICIO` o `TEST`; no lo ejecutes contra una base operativa.

## Archivos de referencia

- [README.md](README.md): documentación de módulos y reglas de negocio.
- [AquaControl_Validacion_SIG.md](../output/analisis/AquaControl_Validacion_SIG.md): resultado de la revisión cartográfica.
- [00_Backup_Original.sql](database/00_Backup_Original.sql): respaldo lógico de la instalación original inspeccionada.
# Diagnóstico en otro equipo

Antes de iniciar AquaControl, compruebe que SQL Server y la base configurada están disponibles:

```powershell
$env:ConnectionStrings__Aqua='Server=localhost\SQLEXPRESS;Database=AquaControlDev;Integrated Security=true;Encrypt=false;TrustServerCertificate=true'
& .\AquaControl\scripts\diagnosticar.ps1
```

Si indica que la base no existe, cree una base nueva con:

```powershell
$env:AQUA_BOOTSTRAP_PASSWORD='DefinaUnaClaveLargaConLetrasYNumeros'
& .\AquaControl\scripts\setup.ps1 -Initialize
```

Para conservar los datos de desarrollo, restaure primero el respaldo de `AquaControlDev` en SQL Server y use el nombre de servidor/instancia real en `ConnectionStrings__Aqua`. No use la ruta `np:\\.\pipe\sql\query` de otro equipo: es una tubería local de este computador.

## Compartir en tu red local

En el equipo que tiene SQL Server y ejecuta AquaControl:

```powershell
& .\AquaControl\scripts\run.ps1 -Host lan -Port 5080 -SandboxPayments
ipconfig
```

Busca la dirección IPv4, por ejemplo `192.168.1.25`. Los demás equipos conectados a la misma red Wi-Fi o cableada abren `http://192.168.1.25:5080`.

Si Windows bloquea el acceso, abre PowerShell como administrador una sola vez y ejecuta:

```powershell
New-NetFirewallRule -DisplayName 'AquaControl LAN 5080' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 5080 -Profile Private
```

Esta opción es para una red local privada. No expongas el puerto 5080 directamente a Internet.
