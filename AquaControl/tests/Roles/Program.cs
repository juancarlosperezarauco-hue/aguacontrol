using AquaControl.Application;
using AquaControl.Domain;
using AquaControl.Infrastructure;
using Microsoft.EntityFrameworkCore;

// SQL real, base exclusiva de pruebas. No utiliza cuentas ni contraseñas operativas.
var connection=Environment.GetEnvironmentVariable("AQUA_ROLES_TEST_CONNECTION")??"Server=np:\\\\.\\pipe\\sql\\query;Database=AquaControlRoleTests;Integrated Security=true;Encrypt=false;TrustServerCertificate=true";
var parsed=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection);
if(parsed.InitialCatalog!="AquaControlRoleTests")throw new Exception("Solo se permite AquaControlRoleTests.");
await using var db=new AquaDb(new DbContextOptionsBuilder<AquaDb>().UseSqlServer(connection,s=>s.UseNetTopologySuite()).Options);
await db.Database.EnsureCreatedAsync();
var service=new AquaService(db);var security=new SecurityService(db,service);
var secret="Test-"+Guid.NewGuid().ToString("N")+"9";
await security.Seed(secret);
var ownerId=await db.Set<User>().Where(u=>u.Login=="aquadmin").Select(u=>u.Id).SingleAsync();
Actor owner=new(ownerId,await service.Permissions(ownerId));
var run=Guid.NewGuid().ToString("N")[..8];int count=0;
void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);count++;}
async Task Denied(Func<Task> action,string message){try{await action();}catch(Exception e)when(e is BusinessException or UnauthorizedAccessException){db.ChangeTracker.Clear();Check(true,message);return;}throw new Exception("Permiso excesivo: "+message);}
async Task<User> UserFor(string role,string suffix){var roleId=await db.Set<Role>().Where(r=>r.Name==role).Select(r=>r.Id).SingleAsync();return await security.Create(owner,"test-"+run+suffix,"FICTICIO "+suffix,secret,roleId,null);}
async Task<Actor> ActorFor(User user)=>new(user.Id,await service.Permissions(user.Id));
var admin=await ActorFor(await UserFor(RolePolicy.Admin,"admin"));
var supervisor=await ActorFor(await UserFor(RolePolicy.Supervisor,"supervisor"));
var reader=await ActorFor(await UserFor(RolePolicy.Reader,"reader"));
var tech=await ActorFor(await UserFor(RolePolicy.Technician,"tech"));
Check(RolePolicy.BaseRoles.Count==5,"Cinco roles base");
Check(admin.Can("security.manage")&&admin.Can("billing.write")&&admin.Can("geo.import"),"Administrador con acceso general");
Check(reader.Can("readings.write")&&reader.Can("billing.write")&&!reader.Can("tariffs.manage")&&!reader.Can("billing.adjust")&&!reader.Can("orders.manage"),"Lector sin gestión de tarifas, saldos ni asignaciones");
Check(!tech.Can("readings.write")&&!tech.Can("billing.write")&&tech.Can("meters.install"),"Técnico sin lectura ni facturación");
await Denied(()=>security.Create(admin,"test-"+run+"owner","FICTICIO",secret,awaitOwnerRole(),null),"Administrador no crea dueños");
int awaitOwnerRole()=>db.Set<Role>().Single(r=>r.Name==RolePolicy.Owner).Id;
await Denied(()=>security.Reset(admin,owner.Id,secret),"Administrador no restablece contraseña del dueño");
await Denied(()=>security.SetRole(admin,owner.Id,awaitOwnerRole(),false),"Administrador no desactiva al dueño");
await Denied(()=>security.SaveRole(admin,null,RolePolicy.Technician,["geo.read"],true),"No duplica roles base");
await Denied(()=>security.SaveRole(admin,null,"TEST ESCALADA "+run,["security.manage"],true),"Rol adicional no adquiere gestión de seguridad");
var custom=await security.SaveRole(admin,null,"TEST ATENCION "+run,["customers.read","geo.read"],true);
Check(custom.Active,"Administrador crea rol administrativo");
await security.SaveRole(admin,custom.Id,"TEST ATENCION EDITADA "+run,["geo.read"],true);Check(true,"Administrador renombra y configura rol adicional");
await Denied(()=>security.SaveRole(admin,awaitOwnerRole(),"TEST DUEÑO "+run,["geo.read"],true),"Roles base protegidos de edición");
await Denied(()=>security.Create(supervisor,"test-"+run+"x","FICTICIO",secret,custom.Id,null),"Supervisor no crea usuarios");
var readingType=await db.Set<WorkType>().SingleAsync(t=>t.Code=="LECTURA");var technicalType=await db.Set<WorkType>().SingleAsync(t=>t.Code=="MANTENIMIENTO");
async Task<WorkOrder> NewOrder(int type)=>await service.CreateOrder(supervisor,new NewOrder(type,null,null,supervisor.Id,"NORMAL",DateTime.UtcNow.AddHours(1),-60.96,-16.38,"FICTICIO ROLES "+run,null));
var readingOrder=await NewOrder(readingType.Id);var technicalOrder=await NewOrder(technicalType.Id);
await Denied(()=>service.Assign(supervisor,readingOrder.Id,new AssignOrder(tech.Id,"TEST",Convert.ToBase64String(readingOrder.Version))),"No asigna lectura a técnico");
readingOrder=await service.Get<WorkOrder>(readingOrder.Id);await service.Assign(supervisor,readingOrder.Id,new AssignOrder(reader.Id,"TEST",Convert.ToBase64String(readingOrder.Version)));Check(true,"Supervisor asigna lectura a lector");
await Denied(()=>service.Assign(supervisor,technicalOrder.Id,new AssignOrder(reader.Id,"TEST",Convert.ToBase64String(technicalOrder.Version))),"No asigna mantenimiento a lector");
technicalOrder=await service.Get<WorkOrder>(technicalOrder.Id);await service.Assign(supervisor,technicalOrder.Id,new AssignOrder(tech.Id,"TEST",Convert.ToBase64String(technicalOrder.Version)));Check(true,"Supervisor asigna mantenimiento a técnico");
await Denied(()=>service.CreateOrder(reader,new NewOrder(readingType.Id,null,null,supervisor.Id,"NORMAL",DateTime.UtcNow,0,0,"TEST",null)),"Operario no crea trabajos");
await Denied(()=>service.OrderAccess(reader,technicalOrder.Id),"Operario no abre orden ajena");
await Denied(()=>service.GenerateInvoices(reader,"2026-11",DateTime.UtcNow.AddDays(15)),"Lector no factura toda la cartera");
await Denied(()=>service.Bill(tech,new NewInvoice(0,0,DateTime.UtcNow.AddDays(15))),"Técnico no emite facturas");
await Denied(()=>service.AddReading(tech,new NewReading(0,"2026-11",10,false,"TEST")),"Técnico no registra consumos");
var readerRoleId=await db.Set<Role>().Where(r=>r.Name==RolePolicy.Reader).Select(r=>r.Id).SingleAsync();
await Denied(()=>security.SetRole(owner,tech.Id,readerRoleId,true),"Cambio de especialidad bloqueado con trabajos abiertos");
// Flujo real de lectura y factura sobre SQL, limitado al servicio asignado.
var client=new Client{Name="FICTICIO roles "+run,Document="TEST-"+run};var account=new Account{Number="TEST-"+run};var connectionRow=new Connection{Code="TEST-"+run,Longitude=-60.96,Latitude=-16.38};var tariff=new Tariff{Name="TEST-"+run,FixedCharge=10,UnitPrice=2};var meter=new Meter{Serial="TEST-"+run};
db.AddRange(client,account,connectionRow,tariff,meter);await db.SaveChangesAsync();
var contract=new Contract{ClientId=client.Id,AccountId=account.Id,ConnectionId=connectionRow.Id,TariffId=tariff.Id};var installation=new MeterInstallation{ConnectionId=connectionRow.Id,MeterId=meter.Id,InitialReading=20};db.AddRange(contract,installation);await db.SaveChangesAsync();
await Denied(()=>service.AddReading(reader,new NewReading(installation.Id,"2026-11",25,false,"TEST")),"Lector no registra consumo de servicio sin trabajo asignado");
var linkedOrder=await service.CreateOrder(supervisor,new NewOrder(readingType.Id,contract.Id,connectionRow.Id,supervisor.Id,"NORMAL",DateTime.UtcNow.AddHours(1),-60.96,-16.38,"FICTICIO LECTURA "+run,null));
await service.Assign(supervisor,linkedOrder.Id,new AssignOrder(reader.Id,"TEST",Convert.ToBase64String(linkedOrder.Version)));
linkedOrder.Status="EN_EJECUCION";await db.SaveChangesAsync();
var reading=await service.AddReading(reader,new NewReading(installation.Id,"2026-11",25,false,"TEST"));
Check(reading.Value-reading.PreviousValue==5,"Lector registra consumo de su orden en ejecución");
var invoice=await service.Bill(reader,new NewInvoice(contract.Id,reading.Id,DateTime.UtcNow.AddDays(15)));
Check(invoice.Total==20,"Lector emite recibo con cálculo real sin editar tarifa");
Check(await service.Contracts(tech).CountAsync(c=>c.Id==contract.Id)==0,"Técnico no consulta servicio ajeno");
await Denied(()=>service.Bill(reader,new NewInvoice(contract.Id,reading.Id,DateTime.UtcNow.AddDays(15))),"No duplica recibo del período");
// La migración retira el perfil genérico y mantiene las cuentas e historial.
var shift=await service.StartShift(reader);Check((await service.StartShift(reader)).Id==shift.Id,"Iniciar jornada es idempotente");
await Denied(()=>service.StartShift(supervisor),"Supervisor no transmite ubicación como operario");
await Denied(()=>service.StartShift(admin),"Administrador monitorea pero no inicia jornada como operario");
await Denied(()=>service.FieldPositions(reader),"Operario no vigila ubicaciones de otros");
await Denied(()=>service.PublishPosition(tech,new FieldPosition(shift.Id,-16.38,-60.96,10,DateTime.UtcNow)),"No suplanta ubicación de otro operario");
await Denied(()=>service.PublishPosition(reader,new FieldPosition(shift.Id,100,-60.96,10,DateTime.UtcNow)),"Rechaza coordenadas inválidas");
await Denied(()=>service.PublishPosition(reader,new FieldPosition(shift.Id,-16.38,-60.96,10,DateTime.UtcNow.AddMinutes(-5))),"Rechaza GPS antiguo");
var captured=DateTime.UtcNow;await service.PublishPosition(reader,new FieldPosition(shift.Id,-16.38,-60.96,10,captured));
var monitoring=System.Text.Json.JsonSerializer.Serialize(await service.FieldPositions(supervisor));Check(monitoring.Contains("EN_LINEA"),"Supervisor recibe GPS reciente");
var shiftRow=await service.Get<FieldShift>(shift.Id);shiftRow.ReceivedAt=DateTime.UtcNow.AddMinutes(-3);await db.SaveChangesAsync();
using(var staleDoc=System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(await service.FieldPositions(supervisor)))){
 var stalePerson=staleDoc.RootElement.GetProperty("operators").EnumerateArray().Single(x=>x.GetProperty("Id").GetInt32()==reader.Id);
 Check(stalePerson.GetProperty("status").GetString()=="SIN_SEÑAL"&&!stalePerson.GetProperty("isLive").GetBoolean(),"Ubicación antigua no aparece como señal en vivo");
}
await Denied(()=>service.PublishPosition(reader,new FieldPosition(shift.Id,-16.38,-60.96,10,captured)),"Rechaza paquetes GPS fuera de orden");
await service.EndShift(reader,shift.Id);await service.EndShift(reader,shift.Id);Check(true,"Finalizar jornada es idempotente");
await Denied(()=>service.PublishPosition(reader,new FieldPosition(shift.Id,-16.38,-60.96,10,DateTime.UtcNow)),"No acepta GPS después de finalizar jornada");
using(var monitorDoc=System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(await service.FieldPositions(supervisor)))){
 var person=monitorDoc.RootElement.GetProperty("operators").EnumerateArray().Single(x=>x.GetProperty("Id").GetInt32()==reader.Id);
 Check(person.GetProperty("latitude").ValueKind==System.Text.Json.JsonValueKind.Null,"No muestra coordenadas de jornada terminada");
}
foreach(var activity in await db.Set<OrderActivity>().Where(a=>a.OrderId==linkedOrder.Id).ToListAsync())activity.Done=true;await db.SaveChangesAsync();
linkedOrder=await service.Get<WorkOrder>(linkedOrder.Id);
await Denied(()=>service.Transition(reader,linkedOrder.Id,new ChangeOrder("FINALIZADA","TEST",Convert.ToBase64String(linkedOrder.Version))),"No finaliza trabajo sin foto");
var pdf=new Evidence{OrderId=linkedOrder.Id,ContentType="application/pdf",FileName="TEST.pdf",StorageKey="TEST.pdf",Hash=new string('A',64),Size=5};await service.AddEvidence(reader,pdf);
linkedOrder=await service.Get<WorkOrder>(linkedOrder.Id);
await Denied(()=>service.Transition(reader,linkedOrder.Id,new ChangeOrder("FINALIZADA","TEST",Convert.ToBase64String(linkedOrder.Version))),"Un PDF no sustituye la fotografía");
var photo=new Evidence{OrderId=linkedOrder.Id,ContentType="image/jpeg",FileName="TEST.jpg",StorageKey="TEST.jpg",Hash=new string('B',64),Size=5};await service.AddEvidence(reader,photo);
await Denied(()=>service.AddEvidence(tech,new Evidence{OrderId=linkedOrder.Id,ContentType="image/jpeg",FileName="TEST-ajeno.jpg",StorageKey="TEST-ajeno.jpg",Hash=new string('C',64),Size=5}),"No adjunta evidencia a un trabajo de otro operario");
Check(photo.ConnectionId==connectionRow.Id&&photo.MeterId==meter.Id&&photo.MeterSerial==meter.Serial,"Foto vinculada al servicio y serie del medidor por el servidor");
var visitShift=await service.StartShift(reader);var visitGps=DateTime.UtcNow;
await service.PublishPosition(reader,new FieldPosition(visitShift.Id,connectionRow.Latitude,connectionRow.Longitude,10,visitGps));
linkedOrder=await service.Get<WorkOrder>(linkedOrder.Id);
var visitRequest=new NewVisit(Guid.NewGuid(),visitShift.Id,"REALIZADO","TEST visita",meter.Serial,25,photo.Id,connectionRow.Latitude,connectionRow.Longitude,10,visitGps,Convert.ToBase64String(linkedOrder.Version));
await Denied(()=>service.Visit(reader,linkedOrder.Id,visitRequest with{CapturedAt=visitGps.AddMinutes(-3)}),"Visita rechaza GPS antiguo");
await Denied(()=>service.Visit(reader,linkedOrder.Id,visitRequest with{ObservedSerial="AJENO"}),"Visita valida serie de medidor");
await Denied(()=>service.Visit(reader,linkedOrder.Id,visitRequest with{EvidenceId=null}),"Visita realizada exige fotografía");
var fieldVisit=await service.Visit(reader,linkedOrder.Id,visitRequest);
Check((await service.Visit(reader,linkedOrder.Id,visitRequest)).Id==fieldVisit.Id,"Reintento de visita no duplica registro");
Check(await db.Set<FieldTrack>().AnyAsync(p=>p.ShiftId==visitShift.Id),"GPS conserva historial de recorrido");
linkedOrder=await service.Get<WorkOrder>(linkedOrder.Id);await service.Transition(reader,linkedOrder.Id,new ChangeOrder("FINALIZADA","TEST",Convert.ToBase64String(linkedOrder.Version)));Check(true,"Finaliza lectura con foto y actividades completas");
// Un trabajo observado tampoco puede cerrarse sin GPS ni prueba de presencia.
technicalOrder=await service.Get<WorkOrder>(technicalOrder.Id);
await service.Transition(tech,technicalOrder.Id,new ChangeOrder("EN_CAMINO","TEST",Convert.ToBase64String(technicalOrder.Version)));
technicalOrder=await service.Get<WorkOrder>(technicalOrder.Id);
await Denied(()=>service.Transition(tech,technicalOrder.Id,new ChangeOrder("NO_REALIZADA","Casa cerrada",Convert.ToBase64String(technicalOrder.Version))),"No realizado exige visita GPS");
var techShift=await service.StartShift(tech);var techGps=DateTime.UtcNow;
await service.PublishPosition(tech,new FieldPosition(techShift.Id,-16.38,-60.96,10,techGps));
var observed=new NewVisit(Guid.NewGuid(),techShift.Id,"OBSERVADO","Casa cerrada","",null,null,-16.38,-60.96,10,techGps,Convert.ToBase64String(technicalOrder.Version));
await Denied(()=>service.Visit(tech,technicalOrder.Id,observed with{Observation=""}),"Observado requiere motivo");
await Denied(()=>service.Visit(tech,technicalOrder.Id,observed with{Accuracy=1000}),"Visita rechaza GPS de baja precisión");
await service.Visit(tech,technicalOrder.Id,observed);
await service.Transition(tech,technicalOrder.Id,new ChangeOrder("NO_REALIZADA","Casa cerrada",Convert.ToBase64String(technicalOrder.Version)));Check(true,"Registra casa cerrada con GPS junto al servicio");
await service.EndShift(tech,techShift.Id);
// Asignación de ruta transaccional con la red de prueba (sin clientes operativos).
var routing=new RoutingService(db,service,new TestStreetNetwork());
var extra=await NewOrder(readingType.Id);
var routeRequest=new PlanRoute(reader.Id,"LECTURA",1,-16.38,-60.961,"A_PIE");
await Denied(()=>routing.Preview(tech,routeRequest),"Operario no planifica ni asigna rutas");
var preview=await routing.Preview(supervisor,routeRequest);
Check(preview.Network.Stops.Length>=1,"Previsualiza pendientes y adicionales por red vial");
await Denied(()=>routing.Save(supervisor,new SaveRoute(routeRequest,"ALTERADO")),"Guardar rechaza una previsualización alterada");
var savedRoute=await routing.Save(supervisor,new SaveRoute(routeRequest,preview.PreviewHash));
Check(await db.Set<RouteStop>().CountAsync(x=>x.RouteId==savedRoute.Id)==preview.Network.Stops.Length,"Guarda paradas con orden y asignaciones");
Check(System.Text.Json.JsonSerializer.Serialize(await routing.Routes(reader)).Contains(savedRoute.NetworkHash),"APK del operario recibe su ruta persistida");
Check(!System.Text.Json.JsonSerializer.Serialize(await routing.Routes(tech)).Contains(savedRoute.NetworkHash),"Técnico no consulta ruta del lector");
var secondPreview=await routing.Preview(supervisor,routeRequest with{AdditionalCount=0});
await routing.Save(supervisor,new SaveRoute(routeRequest with{AdditionalCount=0},secondPreview.PreviewHash));
Check(await db.Set<FieldRoute>().CountAsync(x=>x.OperatorId==reader.Id&&x.Active)==1,"Nueva ruta archiva la anterior sin borrar paradas");
// La migración retira el perfil genérico y mantiene las cuentas e historial.
var oldRole=await db.Set<Role>().SingleOrDefaultAsync(r=>r.Name=="OPERARIO");if(oldRole==null){oldRole=new Role{Name="OPERARIO"};db.Add(oldRole);await db.SaveChangesAsync();}
var legacy=new User{Login="test-"+run+"legacy",Name="FICTICIO legado"};SecurityService.Password(legacy,secret);db.Add(legacy);await db.SaveChangesAsync();db.Add(new UserRole{UserId=legacy.Id,RoleId=oldRole.Id});await db.SaveChangesAsync();
var usersBefore=await db.Set<User>().CountAsync();await security.UpgradeRoles();db.ChangeTracker.Clear();
Check(!await db.Set<Role>().Where(r=>r.Name=="OPERARIO").Select(r=>r.Active).SingleAsync(),"Operario genérico archivado");
Check(await db.Set<User>().CountAsync()==usersBefore,"Migración conserva usuarios");
Check((await service.Permissions(legacy.Id)).Contains("orders.technical"),"Operario sin historial queda técnico");
await security.UpgradeRoles();db.ChangeTracker.Clear();Check(await db.Set<User>().CountAsync()==usersBefore,"Migración repetible sin duplicar usuarios");
Console.WriteLine($"Pruebas correctas: {count}. Base exclusiva: AquaControlRoleTests.");

class TestStreetNetwork:IStreetNetwork {
 public Task<StreetPlan> Plan(RoutePoint start,RoutePoint[] stops,string mode)=>Task.FromResult(new StreetGraph([new StreetLine([new(-60.962,-16.38),new(-60.95,-16.38)])]).Plan(start,stops,mode,"TEST-RED-ROLES"));
}
