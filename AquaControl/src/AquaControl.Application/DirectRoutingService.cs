using AquaControl.Domain;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static AquaControl.Domain.Workflow;
namespace AquaControl.Application;

public record DirectRouteRequest(Guid RequestId,int OperatorId,int SupervisorId,int WorkTypeId,string Kind,int[] ConnectionIds,DateTime ScheduledAt,string Period,double StartLatitude,double StartLongitude,string TravelMode,string Instructions);
public record SaveDirectRoute(DirectRouteRequest Request,string PreviewHash);
public record DirectRoutePreview(DirectRouteRequest Request,string PreviewHash,StreetPlan Network,object[] Orders);

public sealed class DirectRoutingService(IData db,AquaService service,RoutingService routes,IStreetNetwork network,IGeoReference geo) {
 record Candidate(Contract Contract,Client Client,Connection Connection,MeterInstallation Installation,Meter Meter,CutNotice? Notice);
 static readonly string[] Pending=["PENDIENTE","ASIGNADA","REPROGRAMADA","EN_CAMINO"];
 static void Kind(string kind,string? period){
  Require(kind is "CORTE" or "RECONEXION" or "LECTURA","Tipo de asignación inválido.");
  if(kind=="LECTURA")Require(DateTime.TryParseExact(period,"yyyy-MM",CultureInfo.InvariantCulture,DateTimeStyles.None,out _),"Indique período AAAA-MM para lecturas.");
 }
 IQueryable<Candidate> Candidates(string kind,string period,string term="",int? fixedId=null,int[]? selected=null){
  return from contract in db.Set<Contract>()
         join client in db.Set<Client>() on contract.ClientId equals client.Id
         join connection in db.Set<Connection>() on contract.ConnectionId equals connection.Id
         join installation in db.Set<MeterInstallation>() on connection.Id equals installation.ConnectionId
         join meter in db.Set<Meter>() on installation.MeterId equals meter.Id
         from notice in db.Set<CutNotice>().Where(n=>kind=="CORTE"&&n.ContractId==contract.Id&&n.Status=="VIGENTE"&&n.OrderId==null).DefaultIfEmpty()
         where contract.End==null&&client.Active&&installation.End==null&&meter.Active&&connection.FixedCodeId!=null
         where db.Set<Account>().Any(a=>a.Id==contract.AccountId&&a.Active)
         where selected==null||selected.Contains(connection.Id)
         where term.Length<2||client.Name.Contains(term)||connection.Code.Contains(term)||meter.Serial.Contains(term)||connection.FixedCodeId==fixedId
         where kind=="RECONEXION"?connection.Status=="CORTADA":connection.Status=="ACTIVA"||connection.Status=="PENDIENTE_CORTE"
         where kind!="CORTE"||notice!=null
         where !db.Set<WorkOrder>().Any(o=>o.ConnectionId==connection.Id&&o.Status!="CERRADA"&&o.Status!="CANCELADA"&&(kind=="LECTURA"?db.Set<WorkType>().Any(t=>t.Id==o.WorkTypeId&&t.Code=="LECTURA"):o.Effect==kind))
         where kind!="LECTURA"||!db.Set<Reading>().Any(r=>r.InstallationId==installation.Id&&string.Compare(r.Period,period)>=0)
         orderby client.Name,connection.Id
         select new Candidate(contract,client,connection,installation,meter,notice);
 }
 public async Task<object> Eligible(Actor a,string kind,string? period,string? query,int page=1){
  a.Require("orders.manage");a.Require("geo.read");Kind(kind,period);Require(page is >=1 and <=10000,"Página inválida.");
  var term=(query??"").Trim();Require(term.Length<=100,"La búsqueda no puede superar 100 caracteres.");
  var fixedId=term.Length>=2?await geo.FixedCodeId(term):null;
  var candidates=await Candidates(kind,period??"",term,fixedId).AsNoTracking().Skip((page-1)*50).Take(51).ToListAsync();
  var items=new List<object>();foreach(var c in candidates.Take(50)){
   if(c.Notice!=null&&!await service.Eligible(c.Notice))continue;
   items.Add(new{connectionId=c.Connection.Id,connectionCode=c.Connection.Code,client=c.Client.Name,c.Connection.Address,c.Connection.Latitude,c.Connection.Longitude,c.Connection.Status,meter=c.Meter.Serial,fixedCode=await geo.FixedCodeLabel(c.Connection.FixedCodeId!.Value),earliestAt=c.Notice?.ScheduledAt});
  }
  return new{items,page,hasMore=candidates.Count>50,pageSize=50,hint=kind=="CORTE"?"Solo servicios con medidor, aviso vigente sin orden y deuda habilitada para corte.":kind=="RECONEXION"?"Solo servicios cortados con medidor y sin reconexión abierta.":"Solo servicios activos con medidor, sin lectura del período ni otra orden de lectura abierta."};
 }
 static DirectRouteRequest Normalize(DirectRouteRequest r)=>r with{ScheduledAt=r.ScheduledAt.ToUniversalTime(),Period=r.Period?.Trim()??"",Instructions=r.Instructions?.Trim()??""};
 static string Hash(object value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
 async Task<(DirectRoutePreview preview,List<Candidate> candidates,List<WorkOrder> existing)> Prepare(Actor a,DirectRouteRequest request){
  a.Require("orders.manage");a.Require("geo.read");var r=Normalize(request);Kind(r.Kind,r.Period);await routes.CheckOperator(a,r.OperatorId,r.Kind);
  Require(r.RequestId!=Guid.Empty,"Identificador de solicitud obligatorio.");Require(r.ConnectionIds is {Length:>0 and <=50}&&r.ConnectionIds.All(id=>id>0)&&r.ConnectionIds.Distinct().Count()==r.ConnectionIds.Length,"Seleccione entre 1 y 50 servicios diferentes.");
  Require(r.SupervisorId!=r.OperatorId,"El supervisor y el ejecutor deben ser distintos.");
  Require((await service.Get<User>(r.SupervisorId)).Active&&(await service.Permissions(r.SupervisorId)).Contains("orders.manage"),"Supervisor no autorizado.");
  Require(r.ScheduledAt>=DateTime.UtcNow.Date&&r.ScheduledAt<DateTime.UtcNow.AddYears(2),"Fecha programada inválida.");
  Require(double.IsFinite(r.StartLatitude)&&Math.Abs(r.StartLatitude)<=90&&double.IsFinite(r.StartLongitude)&&Math.Abs(r.StartLongitude)<=180,"Marque un punto de partida válido.");
  Require(r.TravelMode is "VEHICULO" or "MOTO" or "A_PIE","Modo de traslado inválido.");Require(r.Instructions.Length is >0 and <=1500,"Indique instrucciones (máximo 1500 caracteres).");
  var type=await service.Get<WorkType>(r.WorkTypeId);Require(type.Active&&(r.Kind=="LECTURA"?type.Code=="LECTURA":type.Effect==r.Kind),"El tipo de trabajo no corresponde al recorrido.");
  var candidates=await Candidates(r.Kind,r.Period,selected:r.ConnectionIds).ToListAsync();
  Require(candidates.Count==r.ConnectionIds.Length,"Uno o más servicios dejaron de ser elegibles. Actualice la búsqueda; no se creó ninguna orden.");
  foreach(var c in candidates)if(c.Notice!=null){Require(r.ScheduledAt>=c.Notice.ScheduledAt,$"Respete la fecha de corte del aviso para {c.Connection.Code} ({c.Notice.ScheduledAt:yyyy-MM-dd HH:mm} UTC).");Require(await service.Eligible(c.Notice),$"La deuda de {c.Connection.Code} ya no justifica el corte.");}
  var assigned=await db.Set<Assignment>().Where(x=>x.OperatorId==r.OperatorId&&x.End==null).OrderBy(x=>x.Id).ToListAsync();var ids=assigned.Select(x=>x.OrderId).ToArray();
  var existing=await db.Set<WorkOrder>().Where(o=>ids.Contains(o.Id)&&Pending.Contains(o.Status)&&(r.Kind=="LECTURA"?db.Set<WorkType>().Any(t=>t.Id==o.WorkTypeId&&t.Code=="LECTURA"):o.Effect==r.Kind)).OrderBy(o=>o.Id).ToListAsync();
  Require(existing.Count+candidates.Count<=50,$"El operario tiene {existing.Count} pendientes de esta especialidad; seleccione como máximo {Math.Max(0,50-existing.Count)} servicios adicionales.");
  var points=existing.Select(o=>new RoutePoint(o.Id,o.Latitude,o.Longitude)).Concat(candidates.OrderBy(c=>c.Connection.Id).Select(c=>new RoutePoint(-c.Connection.Id,c.Connection.Latitude,c.Connection.Longitude))).ToArray();
  var plan=await network.Plan(new(0,r.StartLatitude,r.StartLongitude),points,r.TravelMode);
  var templates=await db.Set<ActivityTemplate>().Where(t=>t.WorkTypeId==type.Id).OrderBy(t=>t.Id).Select(t=>new{t.Id,t.Version}).ToListAsync();
  var hash=Hash(new{request=r,plan.NetworkHash,type.Version,templates,services=candidates.OrderBy(c=>c.Connection.Id).Select(c=>new{id=c.Connection.Id,connection=c.Connection.Version,contract=c.Contract.Version,client=c.Client.Version,meter=c.Meter.Version,installation=c.Installation.Version,notice=c.Notice?.Version}),orders=existing.Select(o=>new{o.Id,o.Version}),assignments=assigned.Where(x=>existing.Any(o=>o.Id==x.OrderId)).Select(x=>new{x.Id,x.OrderId,x.OperatorId})});
  var orders=plan.Stops.Select(stop=>{
   if(stop.OrderId<0){var c=candidates.Single(c=>c.Connection.Id==-stop.OrderId);return (object)new{id=stop.OrderId,number="Nueva orden",status="POR_CREAR",client=c.Client.Name,connectionCode=c.Connection.Code,meter=c.Meter.Serial,stop.Sequence,stop.DistanceMeters,stop.AccessMeters,stop.Latitude,stop.Longitude,isNew=true};}
   var o=existing.Single(o=>o.Id==stop.OrderId);return new{id=o.Id,number=o.Number,status=o.Status,client="Trabajo ya asignado",connectionCode="",meter="",stop.Sequence,stop.DistanceMeters,stop.AccessMeters,stop.Latitude,stop.Longitude,isNew=false};
  }).ToArray();
  return(new(r,hash,plan,orders),candidates,existing);
 }
 public async Task<DirectRoutePreview> Preview(Actor a,DirectRouteRequest request)=>(await Prepare(a,request)).preview;
 public async Task<FieldRoute> Save(Actor a,SaveDirectRoute request){
  a.Require("orders.manage");a.Require("geo.read");var r=Normalize(request.Request);using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  var prior=await db.Set<FieldRoute>().SingleOrDefaultAsync(x=>x.RequestId==r.RequestId);
  if(prior!=null){Require(prior.CreatedById==a.Id&&prior.SourceHash==Hash(r),"Este identificador ya se utilizó para otra asignación.");await tx.CommitAsync();return prior;}
  var (preview,candidates,existing)=await Prepare(a,r);Require(preview.PreviewHash==request.PreviewHash,"Los datos cambiaron. Calcule y revise de nuevo antes de guardar.");
  var newIds=new Dictionary<int,int>();var all=existing.ToList();
  foreach(var c in candidates.OrderBy(c=>c.Connection.Id)){
   var instructions=r.Kind=="LECTURA"?$"LECTURA|{r.Period}|{c.Installation.Id}| {r.Instructions}":r.Instructions;
   var order=await service.CreateOrderInTransaction(a,new(r.WorkTypeId,c.Contract.Id,c.Connection.Id,r.SupervisorId,"NORMAL",r.ScheduledAt,c.Connection.Longitude,c.Connection.Latitude,instructions,c.Notice?.Id));
   newIds[-c.Connection.Id]=order.Id;all.Add(order);
  }
  var plan=preview.Network with{Stops=preview.Network.Stops.Select(s=>s with{OrderId=s.OrderId<0?newIds[s.OrderId]:s.OrderId}).ToArray()};
  var route=await routes.Persist(a,new(r.OperatorId,r.Kind,0,r.StartLatitude,r.StartLongitude,r.TravelMode),plan,all,r.RequestId,Hash(r));
  service.Audit(a,"asignacion.directa",route.Id.ToString(),$"{candidates.Count} órdenes nuevas; {existing.Count} pendientes conservadas.");await db.SaveChangesAsync();await tx.CommitAsync();return route;
 }
}
