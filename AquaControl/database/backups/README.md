# Respaldo de desarrollo AquaControl

`AquaControlDev_20260922.bak` es un respaldo comprimido y verificable de la base de desarrollo. Incluye la estructura, las capas SIG importadas y los datos demostrativos presentes al momento de su creación.

| Propiedad | Valor |
|---|---|
| Tamaño | 7.36 MB |
| SHA-256 | `0924842D7642A63D431A2606875E1F968B5B06272F49AB6E5C378126636A5461` |
| Verificación SQL Server | `RESTORE VERIFYONLY WITH CHECKSUM` correcta |

## Restauración

En SQL Server Management Studio, elija **Restore Database**, seleccione el archivo `.bak` y asigne el nombre `AquaControlDev`.

O mediante `sqlcmd`, ajuste las rutas de datos de su instalación:

```sql
RESTORE DATABASE AquaControlDev
FROM DISK = N'C:\ruta\AquaControlDev_20260922.bak'
WITH MOVE N'AquaControlDev' TO N'C:\SQLData\AquaControlDev.mdf',
     MOVE N'AquaControlDev_log' TO N'C:\SQLData\AquaControlDev_log.ldf',
     RECOVERY, REPLACE;
```

Después configure `ConnectionStrings__Aqua` con su servidor e instancia y ejecute `scripts/diagnosticar.ps1`. Cambie las credenciales iniciales antes de usar la instalación fuera del entorno de desarrollo.
