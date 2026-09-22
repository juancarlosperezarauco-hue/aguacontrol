-- Migración aditiva entre bases: se ejecuta sobre AquaControlDev.
-- Conserva VisorDatosSIG íntegra. Usuarios migrados desactivados hasta revisión/restablecimiento.
-- Requiere 01 aplicado y bootstrap completado. NO usar con órdenes históricas sin extender el mapeo.
USE AquaControlDev;
SET XACT_ABORT ON;
BEGIN TRAN;
IF EXISTS(SELECT 1 FROM VisorDatosSIG.dbo.OrdenesServicio)
    THROW 51001, 'Hay órdenes históricas: se requiere migrar sus estados y relaciones antes del cambio de aplicación.', 1;
IF OBJECT_ID('dbo.MigracionLegado','U') IS NULL
 CREATE TABLE dbo.MigracionLegado(Entidad nvarchar(50) NOT NULL,IdOrigen int NOT NULL,IdDestino int NOT NULL,DatosOriginales nvarchar(max) NULL,Fecha datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),CONSTRAINT PK_MigracionLegado PRIMARY KEY(Entidad,IdOrigen));
DECLARE @id int,@login nvarchar(50),@nombre nvarchar(120),@hash varbinary(32),@salt varbinary(32),@iterations int,@created datetime2,@new int,@json nvarchar(max);
DECLARE users_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT IdUsuario,Login,Nombre,PasswordHash,PasswordSalt,Iteraciones,FechaRegistro FROM VisorDatosSIG.dbo.Usuarios;
OPEN users_cursor;
FETCH NEXT FROM users_cursor INTO @id,@login,@nombre,@hash,@salt,@iterations,@created;
WHILE @@FETCH_STATUS=0 BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.MigracionLegado WHERE Entidad='Usuarios' AND IdOrigen=@id) BEGIN
  IF EXISTS(SELECT 1 FROM dbo.Usuarios WHERE Login=LOWER(@login)) THROW 51002,'Colisión de login: revisar antes de migrar.',1;
  INSERT dbo.Usuarios(Login,Nombre,PasswordHash,PasswordSalt,Iteraciones,Activo,MustChangePassword,FailedAttempts,SecurityVersion,FechaRegistro)
   VALUES(LOWER(@login),@nombre,@hash,@salt,@iterations,0,1,0,0,@created);
  SET @new=SCOPE_IDENTITY();
  SET @json=(SELECT IdUsuario,Login,Nombre,Activo,FechaRegistro FROM VisorDatosSIG.dbo.Usuarios WHERE IdUsuario=@id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  INSERT dbo.MigracionLegado VALUES('Usuarios',@id,@new,@json,SYSUTCDATETIME());
 END;
 FETCH NEXT FROM users_cursor INTO @id,@login,@nombre,@hash,@salt,@iterations,@created;
END;
CLOSE users_cursor;DEALLOCATE users_cursor;
DECLARE @roleName varchar(50);
DECLARE roles_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT IdRol,NombreRol FROM VisorDatosSIG.dbo.Roles;
OPEN roles_cursor;FETCH NEXT FROM roles_cursor INTO @id,@roleName;
WHILE @@FETCH_STATUS=0 BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.MigracionLegado WHERE Entidad='Roles' AND IdOrigen=@id) BEGIN
  IF LEN(@roleName)>42 THROW 51003,'Nombre de rol requiere revisión de longitud.',1;
  INSERT dbo.Roles(NombreRol,Active) VALUES('LEGADO: '+@roleName,1);SET @new=SCOPE_IDENTITY();
  SET @json=(SELECT * FROM VisorDatosSIG.dbo.Roles WHERE IdRol=@id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  INSERT dbo.MigracionLegado VALUES('Roles',@id,@new,@json,SYSUTCDATETIME());
 END;
 FETCH NEXT FROM roles_cursor INTO @id,@roleName;
END;
CLOSE roles_cursor;DEALLOCATE roles_cursor;
INSERT dbo.UsuariosRoles(UserId,RoleId)
 SELECT u.IdDestino,r.IdDestino FROM VisorDatosSIG.dbo.UsuariosRoles ur JOIN dbo.MigracionLegado u ON u.Entidad='Usuarios' AND u.IdOrigen=ur.IdUsuario JOIN dbo.MigracionLegado r ON r.Entidad='Roles' AND r.IdOrigen=ur.IdRol
 WHERE NOT EXISTS(SELECT 1 FROM dbo.UsuariosRoles x WHERE x.UserId=u.IdDestino AND x.RoleId=r.IdDestino);
-- Copia íntegra de cada menú y autorización como archivo de migración relacional.
INSERT dbo.MigracionLegado(Entidad,IdOrigen,IdDestino,DatosOriginales)
 SELECT 'MenuOpciones',m.IdMenu,0,(SELECT x.* FROM VisorDatosSIG.dbo.MenuOpciones x WHERE x.IdMenu=m.IdMenu FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) FROM VisorDatosSIG.dbo.MenuOpciones m WHERE NOT EXISTS(SELECT 1 FROM dbo.MigracionLegado a WHERE a.Entidad='MenuOpciones' AND a.IdOrigen=m.IdMenu);
INSERT dbo.MigracionLegado(Entidad,IdOrigen,IdDestino,DatosOriginales)
 SELECT 'UsuarioMenu',m.IdUsuarioMenu,0,(SELECT x.* FROM VisorDatosSIG.dbo.UsuarioMenu x WHERE x.IdUsuarioMenu=m.IdUsuarioMenu FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) FROM VisorDatosSIG.dbo.UsuarioMenu m WHERE NOT EXISTS(SELECT 1 FROM dbo.MigracionLegado a WHERE a.Entidad='UsuarioMenu' AND a.IdOrigen=m.IdUsuarioMenu);
COMMIT;
SELECT Entidad,COUNT(*) AS Preservados FROM dbo.MigracionLegado GROUP BY Entidad;
