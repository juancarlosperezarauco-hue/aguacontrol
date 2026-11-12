namespace AquaControl.Domain;

public static class RolePolicy {
 public const string Owner="SUPER ADMIN", Admin="ADMINISTRADOR", Supervisor="SUPERVISOR", Reader="OPERARIO LECTURAS Y FACTURACION", Technician="OPERARIO TECNICO";
 public static readonly string[] AllPermissions=["portal.read","customers.read","customers.write","readings.write","billing.read","billing.write","billing.adjust","billing.bulk","tariffs.manage","meters.install","orders.read","orders.manage","orders.execute","orders.readings","orders.technical","catalogs.write","security.manage","reports.read","geo.read","geo.import","field.share","field.monitor"];
 public static readonly Dictionary<string,string[]> BaseRoles=new(){
  [Owner]=AllPermissions,
  [Admin]=AllPermissions,
  [Supervisor]=["portal.read","customers.read","billing.read","orders.read","orders.manage","reports.read","geo.read","field.monitor"],
  [Reader]=["portal.read","billing.read","billing.write","readings.write","orders.read","orders.execute","orders.readings","geo.read","field.share"],
  [Technician]=["portal.read","orders.read","orders.execute","orders.technical","meters.install","geo.read","field.share"]
 };
 public static bool IsBase(string name)=>BaseRoles.ContainsKey(name);
 public static bool CanExecute(IEnumerable<string> permissions,bool reading)=>permissions.Contains("orders.execute")&&permissions.Contains(reading?"orders.readings":"orders.technical");
 // Los roles adicionales son administrativos; los perfiles de campo son fijos.
 public static readonly string[] CustomPermissions=AllPermissions.Except(["security.manage","orders.execute","orders.readings","orders.technical","meters.install","field.share"]).ToArray();
}
