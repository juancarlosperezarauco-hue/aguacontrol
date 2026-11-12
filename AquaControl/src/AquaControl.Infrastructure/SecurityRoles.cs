using AquaControl.Application;
using AquaControl.Domain;
using Microsoft.EntityFrameworkCore;
using System.Data;
using static AquaControl.Domain.Workflow;
namespace AquaControl.Infrastructure;

public partial class SecurityService {
 public Task<bool> IsOwner(int userId)=> (from ur in db.Set<UserRole>() join r in db.Set<Role>() on ur.RoleId equals r.Id where ur.UserId==userId&&r.Active&&r.Name==RolePolicy.Owner select ur).AnyAsync();
 public async Task ManageTarget(Actor a,int userId){a.Require("security.manage");if(await IsOwner(userId)&&!await IsOwner(a.Id))throw new UnauthorizedAccessException("Solo el superadministrador puede gestionar la cuenta del dueño.");}
 async Task AssignableRole(Actor a,int roleId){var role=await service.Get<Role>(roleId);Require(role.Active,"El rol está archivado.");if(role.Name==RolePolicy.Owner&&!await IsOwner(a.Id))throw new UnauthorizedAccessException("Solo el dueño puede asignar Superadministrador.");}
 async Task PreserveOwner(int userId,int roleId,bool active){if(!await IsOwner(userId))return;var role=await service.Get<Role>(roleId);if(active&&role.Name==RolePolicy.Owner)return;Require(await (from u in db.Set<User>() join ur in db.Set<UserRole>() on u.Id equals ur.UserId join r in db.Set<Role>() on ur.RoleId equals r.Id where u.Active&&u.Id!=userId&&r.Active&&r.Name==RolePolicy.Owner select u.Id).AnyAsync(),"Debe conservar al menos un superadministrador activo.");}
 async Task ValidateOpenAssignments(int userId,int roleId,bool active){
  var permissions=await (from rp in db.Set<RolePermission>() join p in db.Set<Permission>() on rp.PermissionId equals p.Id where rp.RoleId==roleId select p.Code).ToListAsync();
  var open=await (from ass in db.Set<Assignment>() join o in db.Set<WorkOrder>() on ass.OrderId equals o.Id join t in db.Set<WorkType>() on o.WorkTypeId equals t.Id where ass.OperatorId==userId&&ass.End==null&&o.Status!="CERRADA"&&o.Status!="CANCELADA" select t.Code).ToListAsync();
  Require(open.All(code=>active&&RolePolicy.CanExecute(permissions,code=="LECTURA")),"Reasigne los trabajos abiertos antes de desactivar al operario o cambiar su especialidad.");
  var supervises=await db.Set<WorkOrder>().AnyAsync(o=>o.SupervisorId==userId&&o.Status!="CERRADA"&&o.Status!="CANCELADA");
  Require(!supervises||(active&&permissions.Contains("orders.manage")),"El usuario supervisa trabajos abiertos; conserve un rol con supervisión hasta cerrarlos.");
 }
 public async Task<Role> SaveRole(Actor a,int? id,string name,string[] permissions,bool active){
  a.Require("security.manage");using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  name=name.Trim().ToUpperInvariant();Require(name.Length is >2 and <=50,"Nombre de rol entre 3 y 50 caracteres.");
  Require(!RolePolicy.IsBase(name)&&!name.StartsWith("OPERARIO")&&!name.StartsWith("LEGADO")&&name!="CLIENTE","Nombre reservado para roles del sistema o archivados.");
  var role=id.HasValue?await service.Get<Role>(id.Value):new Role();Require(!RolePolicy.IsBase(role.Name),"Los cinco roles base son fijos y no se pueden modificar.");Require(!id.HasValue||role.Active,"Un rol archivado no se puede reutilizar.");
  Require(permissions.Length>0&&permissions.All(p=>RolePolicy.CustomPermissions.Contains(p)&&a.Can(p)),"Seleccione permisos administrativos válidos. Los roles de campo y la gestión de seguridad son exclusivos de los perfiles base.");
  Require(!await db.Set<Role>().AnyAsync(x=>x.Name==name&&x.Id!=role.Id),"Ya existe un rol con ese nombre.");
  Require(active||!await db.Set<UserRole>().AnyAsync(x=>x.RoleId==role.Id),"Reasigne los usuarios antes de archivar este rol.");
  role.Name=name;role.Active=active;if(!id.HasValue)db.Add(role);await db.SaveChangesAsync();
  db.RemoveRange(await db.Set<RolePermission>().Where(x=>x.RoleId==role.Id).ToListAsync());await db.SaveChangesAsync();
  foreach(var permission in await db.Set<Permission>().Where(x=>permissions.Contains(x.Code)).ToListAsync())db.Add(new RolePermission{RoleId=role.Id,PermissionId=permission.Id});
  foreach(var user in await db.Set<User>().Where(u=>db.Set<UserRole>().Any(ur=>ur.UserId==u.Id&&ur.RoleId==role.Id)).ToListAsync())user.SecurityVersion++;
  service.Audit(a,"rol.guardar",role.Id.ToString(),name);await db.SaveChangesAsync();await tx.CommitAsync();return role;
 }
 public async Task UpgradeRoles(){
  using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  // Snapshot único previo a la primera actualización; no contiene contraseñas.
  await db.Database.ExecuteSqlRawAsync("""
   IF OBJECT_ID('dbo.RespaldoRolesV2_Usuarios') IS NULL
    SELECT IdUsuario,Activo INTO dbo.RespaldoRolesV2_Usuarios FROM dbo.Usuarios;
   IF OBJECT_ID('dbo.RespaldoRolesV2_Roles') IS NULL BEGIN
    SELECT IdRol,NombreRol,Active INTO dbo.RespaldoRolesV2_Roles FROM dbo.Roles;
    SELECT UserId,RoleId INTO dbo.RespaldoRolesV2_UsuariosRoles FROM dbo.UsuariosRoles;
    SELECT RoleId,PermissionId INTO dbo.RespaldoRolesV2_RolesPermisos FROM dbo.RolesPermisos;
    SELECT UserId,PermissionId,Allow INTO dbo.RespaldoRolesV2_UsuarioPermisos FROM dbo.UsuarioPermisos;
   END
   """);
  foreach(var code in AllPermissions)if(!await db.Set<Permission>().AnyAsync(x=>x.Code==code))db.Add(new Permission{Code=code});await db.SaveChangesAsync();
  foreach(var entry in RolePolicy.BaseRoles){
   var role=await db.Set<Role>().SingleOrDefaultAsync(x=>x.Name==entry.Key);if(role==null){role=new Role{Name=entry.Key};db.Add(role);await db.SaveChangesAsync();}role.Active=true;
   db.RemoveRange(await db.Set<RolePermission>().Where(x=>x.RoleId==role.Id).ToListAsync());await db.SaveChangesAsync();
   foreach(var permission in await db.Set<Permission>().Where(x=>entry.Value.Contains(x.Code)).ToListAsync())db.Add(new RolePermission{RoleId=role.Id,PermissionId=permission.Id});
  }await db.SaveChangesAsync();
  var legacy=await db.Set<Role>().Where(x=>x.Name=="OPERARIO"||x.Name=="CLIENTE"||x.Name.StartsWith("LEGADO:")).ToListAsync();
  foreach(var old in legacy){
   foreach(var assignment in await db.Set<UserRole>().Where(x=>x.RoleId==old.Id).ToListAsync()){
    var target=old.Name.Contains("Lectur",StringComparison.OrdinalIgnoreCase)?RolePolicy.Reader:old.Name.Contains("Administrador",StringComparison.OrdinalIgnoreCase)?RolePolicy.Admin:RolePolicy.Technician;
    if(old.Name=="CLIENTE"){(await service.Get<User>(assignment.UserId)).Active=false;db.Remove(assignment);continue;}
    var types=await (from ass in db.Set<Assignment>() join o in db.Set<WorkOrder>() on ass.OrderId equals o.Id join t in db.Set<WorkType>() on o.WorkTypeId equals t.Id where ass.OperatorId==assignment.UserId select t.Code).Distinct().ToListAsync();
    if(old.Name=="OPERARIO"&&types.Count>0&&types.All(x=>x=="LECTURA"))target=RolePolicy.Reader;
    if(old.Name=="OPERARIO"&&types.Contains("LECTURA")&&types.Any(x=>x!="LECTURA"))throw new BusinessException("El operario "+assignment.UserId+" tiene trabajos de ambas especialidades. Reasigne sus trabajos antes de migrar.");
    var targetId=await db.Set<Role>().Where(x=>x.Name==target).Select(x=>x.Id).SingleAsync();
    db.Remove(assignment);if(!db.Set<UserRole>().Local.Any(x=>x.UserId==assignment.UserId&&x.RoleId==targetId)&&!await db.Set<UserRole>().AnyAsync(x=>x.UserId==assignment.UserId&&x.RoleId==targetId))db.Add(new UserRole{UserId=assignment.UserId,RoleId=targetId});
   }old.Active=false;
  }
  // Elimina concesiones individuales heredadas: la jerarquía determina el acceso.
  db.RemoveRange(await db.Set<UserPermission>().ToListAsync());
  foreach(var user in await db.Set<User>().ToListAsync())user.SecurityVersion++;
  db.Add(new Audit{Action="roles.actualizar",Resource="jerarquia-v2",Detail="Cinco roles base sincronizados; roles anteriores archivados y permisos individuales respaldados."});
  await db.SaveChangesAsync();await tx.CommitAsync();
 }
}
