-- Solo lectura. Ejecutar después de scripts/upgrade-roles.ps1.
SELECT IdRol,NombreRol,Active FROM dbo.Roles ORDER BY Active DESC,IdRol;
SELECT u.IdUsuario,u.Login,u.Activo,r.NombreRol,r.Active AS RolActivo
FROM dbo.Usuarios u LEFT JOIN dbo.UsuariosRoles ur ON ur.UserId=u.IdUsuario
LEFT JOIN dbo.Roles r ON r.IdRol=ur.RoleId ORDER BY u.IdUsuario;
SELECT r.NombreRol,p.Code FROM dbo.Roles r
JOIN dbo.RolesPermisos rp ON rp.RoleId=r.IdRol JOIN dbo.Permisos p ON p.Id=rp.PermissionId
WHERE r.Active=1 ORDER BY r.IdRol,p.Code;
SELECT ur.UserId,ur.RoleId FROM dbo.UsuariosRoles ur JOIN dbo.Roles r ON r.IdRol=ur.RoleId WHERE r.Active=0;
SELECT COUNT(*) AS PermisosIndividuales FROM dbo.UsuarioPermisos;
