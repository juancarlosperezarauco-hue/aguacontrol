# Jerarquía de usuarios y roles

| Rol base | Responsabilidad |
| --- | --- |
| SUPER ADMIN | Dueño, control general y administración de otros dueños. |
| ADMINISTRADOR | Acceso general, usuarios y roles administrativos adicionales. No gestiona la cuenta del dueño. |
| SUPERVISOR | Crea, programa, asigna, reprograma y verifica órdenes y rutas. No crea usuarios. |
| OPERARIO LECTURAS Y FACTURACION | Lecturas y emisión de recibos en sus servicios con una orden de lectura EN_EJECUCION. |
| OPERARIO TECNICO | Instalación, corte, reconexión, mantenimiento y demás trabajos técnicos asignados. |

Los cinco perfiles base son fijos. En Configuración avanzada → Usuarios y permisos, el administrador puede crear, nombrar y configurar roles administrativos adicionales. No pueden convertirse en perfiles de campo ni conceder gestión de usuarios; esas responsabilidades usan los roles base. Solo el dueño puede crear otro superadministrador. No se permite cambiar los propios privilegios en la administración de cuentas.

Los operarios solo acceden a los contratos y datos de servicios de sus órdenes asignadas. El lector puede emitir recibos de sus propias lecturas, pero no cambiar tarifas, ajustar saldos ni facturar masivamente. El técnico registra instalaciones o reemplazos desde su orden EN_EJECUCION; no registra consumos ni emite recibos. El supervisor selecciona únicamente operarios de la especialidad correspondiente. Los trabajos abiertos deben reasignarse antes de cambiar la especialidad o desactivar al ejecutor. Un supervisor con trabajos abiertos debe conservar permisos de supervisión.

## Limpieza de la base existente

PROBLEMA: roles OPERARIO y LEGADO duplican responsabilidades; los permisos individuales pueden conservar accesos antiguos.

CAUSA: coexistían el modelo original y el operativo sin separar lecturas de trabajos técnicos.

SOLUCIÓN: sincronizar los cinco roles base, archivar OPERARIO, CLIENTE y LEGADO; retirar permisos individuales heredados y reasignar las cuentas existentes.

CAMBIO EN BD: se reutilizan Roles, UsuariosRoles, RolesPermisos y Permisos. Se crean cinco tablas RespaldoRolesV2_* que conservan la configuración anterior y el estado de las cuentas, sin contraseñas. No se eliminan usuarios, trabajos, clientes ni cartografía. Las tablas y roles archivados no se eliminan físicamente para conservar procedencia.

IMPACTO: todas las sesiones se invalidan y se debe ingresar nuevamente. Las contraseñas existentes se conservan. Los usuarios CLIENTE del antiguo portal se desactivan; sus vínculos e historial se conservan, y no se convierten en empleados. En esta instalación no había cuentas CLIENTE. Un OPERARIO con historial exclusivamente de LECTURA pasa al perfil lector; sin historial o con trabajos técnicos pasa a técnico. Si tiene historial mixto, la migración se detiene para revisión y revierte su transacción. Los LEGADO se archivan; cuentas de Lecturador se trasladan a lector, de Administrador a administrador y las demás a técnico, para revisión por el administrador.

## Actualizar otra copia

Detener la aplicación, crear un respaldo SQL Server de esa copia y abrir PowerShell desde AquaControl:

```powershell
$env:ConnectionStrings__Aqua='Server=localhost;Database=AquaControlDev;Integrated Security=true;Encrypt=false;TrustServerCertificate=true'
.\scripts\upgrade-roles.ps1
.\scripts\run.ps1
```

La actualización es transaccional y repetible; el snapshot de la primera configuración no se sobrescribe. Ejecutar database/13_Verificar_Jerarquia_Roles.sql para revisar. El rollback de permisos está en database/13_Rollback_Jerarquia_Roles.sql y requiere detener la app y volver al código anterior. Si existen cuentas nuevas, exige conciliación manual. No aplicar un respaldo antiguo sobre operaciones comerciales recientes.

## Pruebas

### Limpieza de cuentas locales de demostración

Después de la migración se archivaron las cuentas locales admin (2), juan (3), pedro (4) y jose21 (5), conservando únicamente aquadmin como cuenta operativa inicial. Se desactivaron sus accesos, se retiraron sus roles y menús, se revocaron sus sesiones y se renombraron sus identificadores de acceso con el prefijo archivado-ID-. Los nombres anteriores quedan libres para cuentas nuevas. La lista de usuarios operativos muestra cuentas vinculadas a un rol activo; los registros históricos archivados se conservan para las relaciones de auditoría.

La orden OT-5AAF2ECAA4DF volvió a PENDIENTE y su asignación anterior se cerró, con un evento de historial para que el supervisor la asigne a un nuevo operario. No se eliminaron clientes, conexiones ni medidores. El respaldo completo previo es database/backups/AquaControlDev_AntesLimpiarUsuarios_20261112.bak (privado, fuera de Git); los metadatos también quedan en RespaldoLimpiezaUsuariosV1_*. El script 14_Limpiar_Cuentas_Prueba_Local.sql apunta únicamente a los IDs y nombres originales de esta instalación, no a usuarios nuevos. No debe utilizarse como una limpieza general en otros equipos.

```powershell
dotnet run --project tests/Roles/Roles.csproj
```

Usa exclusivamente AquaControlRoleTests. Genera cuentas FICTICIO y claves aleatorias en memoria. Comprueba protección del dueño, roles adicionales, permisos negativos, asignación por especialidad, lectura y facturación SQL, y repetibilidad de la migración. Conserva sus datos identificados como prueba y nunca utiliza AquaControlDev.
