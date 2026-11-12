using AquaControl.Application;
using AquaControl.Domain;
using AquaControl.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

var connection="Server=np:\\\\.\\pipe\\sql\\query;Database=AquaControlDispatchTests;Integrated Security=true;Encrypt=false;TrustServerCertificate=true";
if(new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).InitialCatalog!="AquaControlDispatchTests")throw new Exception("Solo base exclusiva de pruebas.");
await using var db=new AquaDb(new DbContextOptionsBuilder<AquaDb>().UseSqlServer(connection,s=>s.UseNetTopologySuite()).Options);
await db.Database.EnsureCreatedAsync();
var geo=new GeoReference(db);var service=new AquaService(db,null,geo);var security=new SecurityService(db,service);var network=new FixtureNetwork();var routing=new RoutingService(db,service,network);var direct=new DirectRoutingService(db,service,routing,network,geo);
var secret="Test-"+Guid.NewGuid().ToString("N")+"9";await security.Seed(secret);var owner=await db.Set<User>().SingleAsync(u=>u.Login=="aquadmin");var actor=new Actor(owner.Id,await service.Permissions(owner.Id));
var run=Guid.NewGuid().ToString("N")[..8];int count=0;
void Check(bool result,string label){if(!result)throw new Exception("FAIL "+label);Console.WriteLine("PASS "+label);count++;}
async Task Denied(Func<Task> f,string label){try{await f();}catch(Exception e)when(e is BusinessException or UnauthorizedAccessException){db.ChangeTracker.Clear();Check(true,label);return;}throw new Exception("FAIL aceptó operación: "+label);}
async Task<Actor> User(string role,string label){var roleId=await db.Set<Role>().Where(r=>r.Name==role).Select(r=>r.Id).SingleAsync();var u=await security.Create(actor,"dispatch-"+run+label,"FICTICIO "+label,secret,roleId,null);return new(u.Id,await service.Permissions(u.Id));}
var supervisor=await User(RolePolicy.Supervisor,"supervisor");var reader=await User(RolePolicy.Reader,"lector");var tech=await User(RolePolicy.Technician,"tecnico");
async Task<(Contract contract,Connection connection,MeterInstallation installation,Meter meter)> Service(string status="ACTIVA",bool installed=true){
 var key="TEST-DISPATCH-"+Guid.NewGuid().ToString("N")[..12];var import=new GeoImport{Layer="codes",Hash=Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N"),SourceFile="FICTICIO"};db.Add(import);await db.SaveChangesAsync();
 var fixedCode=new FixedCode{ImportId=import.Id,Ordinal=1,CodFijo=Random.Shared.Next(100000000,2000000000),Nombre="FICTICIO "+key};db.Add(fixedCode);await db.SaveChangesAsync();
 var client=new Client{Name="FICTICIO "+key};var account=new Account{Number=key};var tariff=new Tariff{Name=key,FixedCharge=10,UnitPrice=2};var meter=new Meter{Serial=key};var conn=new Connection{Code=key,Status=status,Longitude=-60.96,Latitude=-16.38,FixedCodeId=fixedCode.Id};db.AddRange(client,account,tariff,meter,conn);await db.SaveChangesAsync();
 var ct=new Contract{ClientId=client.Id,AccountId=account.Id,ConnectionId=conn.Id,TariffId=tariff.Id,Start=DateTime.UtcNow.AddMonths(-3)};var installation=new MeterInstallation{ConnectionId=conn.Id,MeterId=meter.Id,InitialReading=5};db.Add(ct);if(installed)db.Add(installation);await db.SaveChangesAsync();return(ct,conn,installation,meter);
}
var readingType=await db.Set<WorkType>().SingleAsync(t=>t.Code=="LECTURA");var cutType=await db.Set<WorkType>().SingleAsync(t=>t.Code=="CORTE");var reconnectType=await db.Set<WorkType>().SingleAsync(t=>t.Code=="RECONEXION");
var period=DateTime.UtcNow.ToString("yyyy-MM");
DirectRouteRequest Request(int connectionId,string kind,int typeId,int operatorId)=>new(Guid.NewGuid(),operatorId,supervisor.Id,typeId,kind,[connectionId],DateTime.UtcNow.AddHours(1),period,-16.38,-60.961,"A_PIE","FICTICIO prueba integral "+run);
var active=await Service();var request=Request(active.connection.Id,"LECTURA",readingType.Id,reader.Id);
await Denied(()=>direct.Preview(reader,request),"Operario no genera asignaciones directas");
await Denied(()=>direct.Preview(supervisor,request with{OperatorId=tech.Id}),"Lectura solo se asigna al lector");
await Denied(()=>direct.Preview(supervisor,request with{ConnectionIds=[active.connection.Id,active.connection.Id]}),"Rechaza servicios repetidos");
var noMeter=await Service(installed:false);await Denied(()=>direct.Preview(supervisor,Request(noMeter.connection.Id,"LECTURA",readingType.Id,reader.Id)),"No envía a leer un servicio sin medidor");
var pending=await Service("PENDIENTE_INSTALACION");await Denied(()=>direct.Preview(supervisor,Request(pending.connection.Id,"LECTURA",readingType.Id,reader.Id)),"No envía a leer instalaciones pendientes");
var disconnected=await Service("CORTADA");await Denied(()=>direct.Preview(supervisor,Request(disconnected.connection.Id,"LECTURA",readingType.Id,reader.Id)),"No envía lecturas a servicios cortados");
await Denied(()=>direct.Preview(supervisor,Request(active.connection.Id,"CORTE",cutType.Id,tech.Id)),"Corte sin aviso no se crea desde el mapa");
var before=await db.Set<WorkOrder>().CountAsync();var preview=await direct.Preview(supervisor,request);
Check(await db.Set<WorkOrder>().CountAsync()==before,"Vista previa no crea órdenes ni asignaciones");
await Denied(()=>direct.Save(supervisor,new(request,"ALTERADO")),"No guarda una ruta sin previsualización válida");
Check(await db.Set<WorkOrder>().CountAsync()==before,"Una validación fallida conserva la BD");
var saved=await direct.Save(supervisor,new(request,preview.PreviewHash));var stop=await db.Set<RouteStop>().SingleAsync(s=>s.RouteId==saved.Id);
Check(await db.Set<WorkOrder>().CountAsync()==before+1,"Crear ruta genera una única orden");
Check((await db.Set<WorkOrder>().SingleAsync(o=>o.Id==stop.OrderId)).Instructions.StartsWith("LECTURA|"+period+"|"),"Orden conserva período e instalación de la lectura");
Check(await db.Set<Assignment>().AnyAsync(a=>a.OrderId==stop.OrderId&&a.OperatorId==reader.Id&&a.End==null),"Asigna el trabajo al lector elegido");
Check((await direct.Save(supervisor,new(request,preview.PreviewHash))).Id==saved.Id&&await db.Set<WorkOrder>().CountAsync()==before+1,"Reintento de red no duplica órdenes");
await Denied(()=>direct.Save(supervisor,new(request with{Instructions="ALTERADAS"},preview.PreviewHash)),"Mismo identificador no admite otro contenido");
await Denied(()=>direct.Preview(supervisor,request with{RequestId=Guid.NewGuid()}),"No vuelve a generar una lectura abierta del mismo servicio");
async Task<WorkOrder> Order(int id)=>await db.Set<WorkOrder>().SingleAsync(o=>o.Id==id);
async Task State(Actor a,int id,string status){var o=await Order(id);await service.Transition(a,id,new(status,"FICTICIO prueba",Convert.ToBase64String(o.Version)));}
async Task Finish(Actor oper,int id,Meter meter,decimal reading){
 var o=await Order(id);if(o.Status=="ASIGNADA"){await State(oper,id,"EN_CAMINO");await State(oper,id,"EN_EJECUCION");o=await Order(id);}
 foreach(var activity in await db.Set<OrderActivity>().Where(x=>x.OrderId==id).ToListAsync()){
  await service.Activity(oper,id,activity.Id,true,"FICTICIO actividad realizada",Convert.ToBase64String(activity.Version));
  if(activity.EvidenceRequired)await service.AddEvidence(oper,new Evidence{OrderId=id,ActivityId=activity.Id,FileName="FICTICIO.jpg",StorageKey=Guid.NewGuid()+".jpg",ContentType="image/jpeg",Hash=new string('A',64),Size=100});
 }
 var photo=new Evidence{OrderId=id,FileName="FICTICIO-medidor.jpg",StorageKey=Guid.NewGuid()+".jpg",ContentType="image/jpeg",Hash=new string('B',64),Size=100};await service.AddEvidence(oper,photo);
 var shift=await service.StartShift(oper);var captured=DateTime.UtcNow;await service.PublishPosition(oper,new(shift.Id,o.Latitude,o.Longitude,10,captured));
 o=await Order(id);await service.Visit(oper,id,new(Guid.NewGuid(),shift.Id,"REALIZADO","FICTICIO visita",meter.Serial,reading,photo.Id,o.Latitude,o.Longitude,10,captured,Convert.ToBase64String(o.Version)));
 if(o.Effect=="CORTE"){o=await Order(id);await service.AuthorizeCut(oper,id,Convert.ToBase64String(o.Version));}
 if(o.Effect is "CORTE" or "RECONEXION"){o=await Order(id);await service.RecordEffect(oper,id,Convert.ToBase64String(o.Version));}
 await State(oper,id,"FINALIZADA");await State(supervisor,id,"VERIFICADA");await State(supervisor,id,"CERRADA");await service.EndShift(oper,shift.Id);
}
// Real database flow for reading -> receipt -> visit -> verification -> closure.
await State(reader,stop.OrderId,"EN_CAMINO");await State(reader,stop.OrderId,"EN_EJECUCION");
var value=await service.AddReading(reader,new(active.installation.Id,period,10,false,"FICTICIO consumo"));var invoice=await service.Bill(reader,new(active.contract.Id,value.Id,DateTime.UtcNow.AddDays(15)));
Check(invoice.Consumption==5&&invoice.Total==20,"Consumo y recibo calculados a partir de lectura y tarifa");
await Finish(reader,stop.OrderId,active.meter,10);
Check((await Order(stop.OrderId)).Status=="CERRADA","Lectura, evidencia, GPS, verificación y cierre completos");
var cut=await Service();var policy=new CollectionPolicy{Enabled=true,Threshold=1,OverdueDays=0,NoticeDays=0};db.Add(policy);await db.SaveChangesAsync();
db.Add(new Invoice{Number="TEST-DISPATCH-"+run,ContractId=cut.contract.Id,Period=DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM"),DueAt=DateTime.UtcNow.AddDays(-2),Total=50,HolderName="FICTICIO"});await db.SaveChangesAsync();
var notice=await service.CreateNotice(supervisor,cut.contract.Id);var cutRequest=Request(cut.connection.Id,"CORTE",cutType.Id,tech.Id) with{ScheduledAt=DateTime.UtcNow};
var cutPreview=await direct.Preview(supervisor,cutRequest);var cutRoute=await direct.Save(supervisor,new(cutRequest,cutPreview.PreviewHash));var cutStop=await db.Set<RouteStop>().SingleAsync(s=>s.RouteId==cutRoute.Id);
await Finish(tech,cutStop.OrderId,cut.meter,7);Check((await service.Get<Connection>(cut.connection.Id)).Status=="CORTADA","Corte requiere aviso, deuda, GPS, foto, lectura y autorización");
var reconnectRequest=Request(cut.connection.Id,"RECONEXION",reconnectType.Id,tech.Id);var reconnectPreview=await direct.Preview(supervisor,reconnectRequest);var reconnectRoute=await direct.Save(supervisor,new(reconnectRequest,reconnectPreview.PreviewHash));var reconnectStop=await db.Set<RouteStop>().SingleAsync(s=>s.RouteId==reconnectRoute.Id);
await Finish(tech,reconnectStop.OrderId,cut.meter,7);Check((await service.Get<Connection>(cut.connection.Id)).Status=="ACTIVA","Reconexión completa restaura el servicio y cierra la orden");
var changed=await Service();var changedRequest=Request(changed.connection.Id,"LECTURA",readingType.Id,reader.Id);var changedPreview=await direct.Preview(supervisor,changedRequest);changed.connection.Address="FICTICIO dato actualizado";await db.SaveChangesAsync();
var countBefore=await db.Set<WorkOrder>().CountAsync();await Denied(()=>direct.Save(supervisor,new(changedRequest,changedPreview.PreviewHash)),"Cambio posterior a la vista previa exige recalcular");Check(await db.Set<WorkOrder>().CountAsync()==countBefore,"No quedan órdenes parciales ante un cambio concurrente");
Console.WriteLine($"Pruebas de asignación directa: {count}. Base exclusiva AquaControlDispatchTests.");

class FixtureNetwork:IStreetNetwork {
 public Task<StreetPlan> Plan(RoutePoint start,RoutePoint[] stops,string mode)=>Task.FromResult(new StreetGraph([new StreetLine([new(-60.962,-16.38),new(-60.95,-16.38)])]).Plan(start,stops,mode,"TEST-RED-ASIGNACION-DIRECTA"));
}
