# Respaldo de desarrollo AquaControl

`AquaControlDev_Equipo_20261111.bak` es el respaldo vigente para compartir de forma privada con el equipo. Incluye la estructura, las capas SIG importadas, el padrón de 6.271 servicios y la configuración presente al momento de su creación.

Los archivos `.bak` están excluidos de Git por seguridad; distribuya este respaldo por OneDrive, Google Drive, USB o un canal privado, nunca mediante GitHub público.

| Propiedad | Valor |
|---|---|
| Tamaño | 9.13 MB |
| SHA-256 | `118EDD56CEC53F91F59C00C2DB069B695EBF1A8222087DD53690D8E3D8CB41A5` |
| Verificación SQL Server | `RESTORE VERIFYONLY WITH CHECKSUM` correcta |

## Restauración

En SQL Server Management Studio, elija **Restore Database**, seleccione el archivo `.bak` y asigne el nombre `AquaControlDev`.

O mediante `sqlcmd`, ajuste las rutas de datos de su instalación:

```sql
RESTORE DATABASE AquaControlDev
FROM DISK = N'C:\ruta\AquaControlDev_Equipo_20261111.bak'
WITH MOVE N'AquaControlDev' TO N'C:\SQLData\AquaControlDev.mdf',
     MOVE N'AquaControlDev_log' TO N'C:\SQLData\AquaControlDev_log.ldf',
     RECOVERY, REPLACE;
```

Después configure `ConnectionStrings__Aqua` con su servidor e instancia y ejecute `scripts/diagnosticar.ps1`. Cambie las credenciales iniciales antes de usar la instalación fuera del entorno de desarrollo.
