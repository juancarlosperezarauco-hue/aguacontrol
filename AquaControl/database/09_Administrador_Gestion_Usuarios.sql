/*
   AquaControl — habilita al rol ADMINISTRADOR para crear y administrar cuentas.
   Es incremental: no elimina roles, usuarios ni permisos existentes.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @RolAdministrador int=(SELECT IdRol FROM dbo.Roles WHERE NombreRol=N'ADMINISTRADOR');
DECLARE @PermisoGestionUsuarios int=(SELECT Id FROM dbo.Permisos WHERE Code=N'security.manage');

IF @RolAdministrador IS NULL THROW 51020, 'No existe el rol ADMINISTRADOR.', 1;
IF @PermisoGestionUsuarios IS NULL THROW 51021, 'No existe el permiso security.manage.', 1;

IF NOT EXISTS (
    SELECT 1 FROM dbo.RolesPermisos
    WHERE RoleId=@RolAdministrador AND PermissionId=@PermisoGestionUsuarios
)
    INSERT dbo.RolesPermisos(RoleId,PermissionId)
    VALUES(@RolAdministrador,@PermisoGestionUsuarios);

COMMIT TRANSACTION;

