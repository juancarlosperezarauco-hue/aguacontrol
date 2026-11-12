-- EJECUCIÓN MANUAL. Detener AquaControl y volver al código anterior antes de usar.
-- Restaura exclusivamente roles/permisos. No restaura una base antigua sobre datos nuevos.
-- Se bloquea si hay usuarios nuevos para evitar perder sus asignaciones.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.RespaldoRolesV2_Roles') IS NULL THROW 51000,'No existe respaldo previo de roles.',1;
IF EXISTS(SELECT 1 FROM dbo.Usuarios u WHERE NOT EXISTS(SELECT 1 FROM dbo.RespaldoRolesV2_Usuarios b WHERE b.IdUsuario=u.IdUsuario))
 THROW 51000,'Hay usuarios nuevos o sin rol respaldado. Concilie sus roles antes del rollback.',1;
DELETE FROM dbo.UsuarioPermisos;
INSERT dbo.UsuarioPermisos(UserId,PermissionId,Allow) SELECT UserId,PermissionId,Allow FROM dbo.RespaldoRolesV2_UsuarioPermisos;
DELETE FROM dbo.UsuariosRoles;
INSERT dbo.UsuariosRoles(UserId,RoleId) SELECT UserId,RoleId FROM dbo.RespaldoRolesV2_UsuariosRoles;
DELETE FROM dbo.RolesPermisos;
INSERT dbo.RolesPermisos(RoleId,PermissionId) SELECT RoleId,PermissionId FROM dbo.RespaldoRolesV2_RolesPermisos;
UPDATE dbo.Roles SET Active=0;
UPDATE r SET NombreRol=b.NombreRol,Active=b.Active FROM dbo.Roles r JOIN dbo.RespaldoRolesV2_Roles b ON b.IdRol=r.IdRol;
UPDATE u SET Activo=b.Activo,SecurityVersion=SecurityVersion+1 FROM dbo.Usuarios u JOIN dbo.RespaldoRolesV2_Usuarios b ON b.IdUsuario=u.IdUsuario;
COMMIT;
