/*
  Integra las cuentas de ScriptDatabaseV13 en AquaControl.
  Conserva aquadmin como recuperación técnica y desactiva la cuenta demostrativa jose21.
  No elimina usuarios ni contraseñas: genera un respaldo antes de cambiar roles o estados.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.RespaldoUsuariosAntesV13',N'U') IS NULL
BEGIN
    SELECT IdUsuario,Login,Nombre,PasswordHash,PasswordSalt,Iteraciones,Activo,
           MustChangePassword,FailedAttempts,LockedUntil,SecurityVersion,FechaRegistro
    INTO dbo.RespaldoUsuariosAntesV13
    FROM dbo.Usuarios;

    SELECT ur.Id,ur.UserId,ur.RoleId,r.NombreRol AS RolAnterior
    INTO dbo.RespaldoUsuariosRolesAntesV13
    FROM dbo.UsuariosRoles ur
    INNER JOIN dbo.Roles r ON r.IdRol=ur.RoleId;
END;

DECLARE @Administrador int=(SELECT IdRol FROM dbo.Roles WHERE NombreRol=N'ADMINISTRADOR');
DECLARE @Operario int=(SELECT IdRol FROM dbo.Roles WHERE NombreRol=N'OPERARIO');
IF @Administrador IS NULL THROW 51040, 'No existe el rol ADMINISTRADOR.', 1;
IF @Operario IS NULL THROW 51041, 'No existe el rol OPERARIO.', 1;

DECLARE @UsuariosOrigen TABLE(Login nvarchar(50),RoleId int);
INSERT @UsuariosOrigen(Login,RoleId)
VALUES(N'admin',@Administrador),(N'juan',@Operario),(N'pedro',@Operario);

IF EXISTS(
    SELECT 1 FROM @UsuariosOrigen s
    WHERE NOT EXISTS(SELECT 1 FROM dbo.Usuarios u WHERE u.Login=s.Login)
) THROW 51042, 'Falta una cuenta de origen: admin, juan o pedro.', 1;

DELETE ur
FROM dbo.UsuariosRoles ur
INNER JOIN dbo.Usuarios u ON u.IdUsuario=ur.UserId
INNER JOIN @UsuariosOrigen s ON s.Login=u.Login;

INSERT dbo.UsuariosRoles(UserId,RoleId)
SELECT u.IdUsuario,s.RoleId
FROM dbo.Usuarios u
INNER JOIN @UsuariosOrigen s ON s.Login=u.Login;

UPDATE u
   SET Activo=1,
       MustChangePassword=1,
       FailedAttempts=0,
       LockedUntil=NULL,
       SecurityVersion=SecurityVersion+1
FROM dbo.Usuarios u
INNER JOIN @UsuariosOrigen s ON s.Login=u.Login;

/* Cuenta demostrativa de desarrollo: queda preservada, pero sin acceso operativo. */
UPDATE dbo.Usuarios
   SET Activo=0,
       SecurityVersion=SecurityVersion+1
 WHERE Login=N'jose21';

COMMIT TRANSACTION;

