-- Limpieza autorizada de las cuatro cuentas existentes de esta instalación.
-- Archiva cuentas para conservar FKs e historial y libera sus nombres de acceso.
-- Crear respaldo completo antes de ejecutar. Nunca elimina clientes ni medidores.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;
DECLARE @targets TABLE(Id int PRIMARY KEY,Login nvarchar(50));
INSERT @targets SELECT IdUsuario,Login FROM dbo.Usuarios
WHERE (IdUsuario=2 AND Login=N'admin') OR (IdUsuario=3 AND Login=N'juan')
 OR (IdUsuario=4 AND Login=N'pedro') OR (IdUsuario=5 AND Login=N'jose21');
IF NOT EXISTS(SELECT 1 FROM dbo.Usuarios u JOIN dbo.UsuariosRoles ur ON ur.UserId=u.IdUsuario JOIN dbo.Roles r ON r.IdRol=ur.RoleId WHERE u.IdUsuario=1 AND u.Login=N'aquadmin' AND u.Activo=1 AND r.NombreRol=N'SUPER ADMIN' AND r.Active=1)
 THROW 51000,'Debe conservarse aquadmin como dueño activo.',1;
IF EXISTS(SELECT 1 FROM dbo.OrdenesServicio o JOIN dbo.Asignaciones a ON a.OrderId=o.IdOrden JOIN @targets t ON t.Id=a.OperatorId WHERE a.[End] IS NULL AND o.Status IN('EN_CAMINO','EN_EJECUCION'))
 THROW 51000,'Hay tareas en curso. Finalice o reprograme antes de archivar esas cuentas.',1;
IF EXISTS(SELECT 1 FROM dbo.OrdenesServicio o JOIN @targets t ON t.Id=o.SupervisorId WHERE o.Status NOT IN('CERRADA','CANCELADA'))
 THROW 51000,'Una cuenta supervisa órdenes abiertas. Reasigne su supervisión antes de limpiar.',1;
IF OBJECT_ID('dbo.RespaldoLimpiezaUsuariosV1_Cuentas') IS NULL BEGIN
 SELECT u.IdUsuario,u.Login,u.Activo,u.SecurityVersion INTO dbo.RespaldoLimpiezaUsuariosV1_Cuentas FROM dbo.Usuarios u JOIN @targets t ON t.Id=u.IdUsuario;
 SELECT ur.UserId,ur.RoleId INTO dbo.RespaldoLimpiezaUsuariosV1_Roles FROM dbo.UsuariosRoles ur JOIN @targets t ON t.Id=ur.UserId;
 SELECT a.Id,a.OrderId,a.OperatorId,a.[End] INTO dbo.RespaldoLimpiezaUsuariosV1_Asignaciones FROM dbo.Asignaciones a JOIN @targets t ON t.Id=a.OperatorId;
END;
DECLARE @orders TABLE(Id int PRIMARY KEY);
INSERT @orders SELECT DISTINCT o.IdOrden FROM dbo.OrdenesServicio o JOIN dbo.Asignaciones a ON a.OrderId=o.IdOrden JOIN @targets t ON t.Id=a.OperatorId WHERE a.[End] IS NULL AND o.Status='ASIGNADA';
INSERT dbo.EventosOrden(OrderId,UserId,Action,Detail,CreatedAt)
 SELECT Id,1,N'orden.limpieza_cuenta',N'Cuenta anterior archivada. Orden devuelta a pendiente para asignar un nuevo operario.',SYSUTCDATETIME() FROM @orders;
UPDATE o SET Status='PENDIENTE',CutAuthorizedAt=NULL FROM dbo.OrdenesServicio o JOIN @orders t ON t.Id=o.IdOrden;
UPDATE a SET [End]=SYSUTCDATETIME() FROM dbo.Asignaciones a JOIN @targets t ON t.Id=a.OperatorId WHERE a.[End] IS NULL;
DELETE up FROM dbo.UsuarioPermisos up JOIN @targets t ON t.Id=up.UserId;
DELETE ur FROM dbo.UsuariosRoles ur JOIN @targets t ON t.Id=ur.UserId;
DELETE um FROM dbo.UsuarioMenu um JOIN @targets t ON t.Id=um.UserId;
UPDATE u SET Login=CONCAT(N'archivado-',u.IdUsuario,N'-',LEFT(u.Login,30)),Activo=0,SecurityVersion=SecurityVersion+1 FROM dbo.Usuarios u JOIN @targets t ON t.Id=u.IdUsuario;
IF EXISTS(SELECT 1 FROM @targets) INSERT dbo.Bitacora(UserId,Action,Resource,Detail,CreatedAt)
 VALUES(1,N'usuarios.limpiar',N'cuentas-prueba',N'admin, juan, pedro y jose21 archivados; roles retirados; nombres liberados; dueño e historial conservados.',SYSUTCDATETIME());
COMMIT;
SELECT u.IdUsuario,u.Login,u.Activo,r.NombreRol FROM dbo.Usuarios u JOIN dbo.UsuariosRoles ur ON ur.UserId=u.IdUsuario JOIN dbo.Roles r ON r.IdRol=ur.RoleId WHERE r.Active=1;
