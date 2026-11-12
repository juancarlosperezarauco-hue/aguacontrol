using AquaControl.Domain;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static AquaControl.Domain.Workflow;
namespace AquaControl.Application;

public record RoutePoint(int Id,double Latitude,double Longitude);
public record StreetStop(int OrderId,double Latitude,double Longitude,int Sequence,double DistanceMeters,double AccessMeters);
public record StreetPlan(string NetworkHash,double[][] Path,StreetStop[] Stops,double DistanceMeters,string[] Warnings);
public interface IStreetNetwork { Task<StreetPlan> Plan(RoutePoint start,RoutePoint[] stops,string mode); }
public record PlanRoute(int OperatorId,string Kind,int AdditionalCount,double StartLatitude,double StartLongitude,string TravelMode);
public record SaveRoute(PlanRoute Plan,string PreviewHash);
public record RoutePreview(PlanRoute Request,string PreviewHash,StreetPlan Network,object[] Orders);

public class RoutingService(IData db,AquaService service,IStreetNetwork network) {
 static readonly string[] Pending=["PENDIENTE","ASIGNADA","REPROGRAMADA","EN_CAMINO"];
 internal async Task CheckOperator(Actor a,int operatorId,string kind){
  a.Require("orders.manage");Require(kind is "CORTE" or "RECONEXION" or "LECTURA","Tipo de recorrido inválido.");
  var role=kind=="LECTURA"?RolePolicy.Reader:RolePolicy.Technician;
  Require(await (from ur in db.Set<UserRole>() join r in db.Set<Role>() on ur.RoleId equals r.Id join u in db.Set<User>() on ur.UserId equals u.Id where u.Id==operatorId&&u.Active&&r.Active&&r.Name==role select u.Id).AnyAsync(),"Seleccione un operario de la especialidad correspondiente.");
 }
 async Task<(RoutePreview preview,List<WorkOrder> selected)> Prepare(Actor a,PlanRoute r){
  await CheckOperator(a,r.OperatorId,r.Kind);
  Require(r.AdditionalCount is >=0 and <=50,"La cantidad adicional debe estar entre 0 y 50.");
  Require(double.IsFinite(r.StartLatitude)&&double.IsFinite(r.StartLongitude)&&Math.Abs(r.StartLatitude)<=90&&Math.Abs(r.StartLongitude)<=180,"Marque un punto de partida válido.");
  Require(r.TravelMode is "VEHICULO" or "MOTO" or "A_PIE","Modo de traslado inválido.");
  var assigned=await db.Set<Assignment>().Where(x=>x.OperatorId==r.OperatorId&&x.End==null).Select(x=>x.OrderId).ToListAsync();
  var matching=db.Set<WorkOrder>().Where(o=>r.Kind=="LECTURA"?db.Set<WorkType>().Any(t=>t.Id==o.WorkTypeId&&t.Code=="LECTURA"):o.Effect==r.Kind);
  var existing=await matching.Where(o=>assigned.Contains(o.Id)&&Pending.Contains(o.Status)).OrderBy(o=>o.Id).ToListAsync();
  Require(existing.Count<=50,"El operario tiene más de 50 pendientes: cierre trabajos antes de planificar otra ruta.");
  var candidates=await matching.Where(o=>(o.Status=="PENDIENTE"||o.Status=="REPROGRAMADA")&&o.SupervisorId!=r.OperatorId&&!db.Set<Assignment>().Any(x=>x.OrderId==o.Id&&x.End==null)).OrderBy(o=>o.ScheduledAt).ThenBy(o=>o.Id).Take(200).ToListAsync();
  // Candidate selection is bounded. Ordering uses road distance, never straight-line links between properties.
  var needed=Math.Min(r.AdditionalCount,50-existing.Count);
  var additional=candidates.OrderBy(o=>Math.Pow(o.Latitude-r.StartLatitude,2)+Math.Pow((o.Longitude-r.StartLongitude)*Math.Cos(r.StartLatitude*Math.PI/180),2)).Take(needed).ToList();
  var selected=existing.Concat(additional).ToList();Require(selected.Count>0,"No hay órdenes pendientes para esta selección. Cree primero las órdenes y sus avisos cuando correspondan.");
  var plan=await network.Plan(new(0,r.StartLatitude,r.StartLongitude),selected.Select(o=>new RoutePoint(o.Id,o.Latitude,o.Longitude)).ToArray(),r.TravelMode);
  var clients=await (from o in matching where selected.Select(x=>x.Id).Contains(o.Id) join c in db.Set<Contract>() on o.ContractId equals c.Id join cl in db.Set<Client>() on c.ClientId equals cl.Id select new{o.Id,cl.Name}).ToDictionaryAsync(x=>x.Id,x=>x.Name);
  var canonical=JsonSerializer.Serialize(new{r,plan.NetworkHash,orders=selected.OrderBy(x=>x.Id).Select(x=>new{x.Id,x.Status,x.Latitude,x.Longitude,x.ScheduledAt,Version=Convert.ToBase64String(x.Version)}),assignments=await db.Set<Assignment>().Where(x=>selected.Select(o=>o.Id).Contains(x.OrderId)&&x.End==null).OrderBy(x=>x.Id).Select(x=>new{x.Id,x.OrderId,x.OperatorId}).ToListAsync()});
  var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
  var warnings=plan.Warnings.ToList();if(additional.Count<r.AdditionalCount)warnings.Add($"Solo se agregan {additional.Count} órdenes de las {r.AdditionalCount} solicitadas (disponibilidad y límite de 50 paradas).");
  plan=plan with{Warnings=warnings.ToArray()};
  return(new(r,hash,plan,plan.Stops.Select(s=>{var o=selected.Single(x=>x.Id==s.OrderId);return (object)new{o.Id,o.Number,o.Status,o.Instructions,o.ConnectionId,o.WorkTypeId,client=clients.GetValueOrDefault(o.Id,"Trabajo de área"),s.Sequence,s.DistanceMeters,s.AccessMeters,o.Latitude,o.Longitude};}).ToArray()),selected);
 }
 public async Task<RoutePreview> Preview(Actor a,PlanRoute r)=>(await Prepare(a,r)).preview;
 public async Task<object> Options(Actor a,int operatorId,string kind){
  await CheckOperator(a,operatorId,kind);
  var matching=db.Set<WorkOrder>().Where(o=>kind=="LECTURA"?db.Set<WorkType>().Any(t=>t.Id==o.WorkTypeId&&t.Code=="LECTURA"):o.Effect==kind);
  var pending=await matching.Where(o=>Pending.Contains(o.Status)&&db.Set<Assignment>().Any(x=>x.OrderId==o.Id&&x.OperatorId==operatorId&&x.End==null)).OrderBy(o=>o.ScheduledAt).Select(o=>new{o.Id,o.Number,o.Status}).ToListAsync();
  return new{pending,available=await matching.CountAsync(o=>(o.Status=="PENDIENTE"||o.Status=="REPROGRAMADA")&&o.SupervisorId!=operatorId&&!db.Set<Assignment>().Any(x=>x.OrderId==o.Id&&x.End==null))};
 }
 public async Task<FieldRoute> Save(Actor a,SaveRoute r){
  a.Require("orders.manage");using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  var (preview,selected)=await Prepare(a,r.Plan);Require(preview.PreviewHash==r.PreviewHash,"Los trabajos, asignaciones o vías cambiaron. Calcule y revise la ruta otra vez.");
  var route=await Persist(a,r.Plan,preview.Network,selected);await tx.CommitAsync();return route;
 }
 internal async Task<FieldRoute> Persist(Actor a,PlanRoute request,StreetPlan street,List<WorkOrder> selected,Guid? requestId=null,string sourceHash=""){
  a.Require("orders.manage");Require(db.Database.CurrentTransaction!=null,"Guardar una ruta requiere transacción.");
  foreach(var old in await db.Set<FieldRoute>().Where(x=>x.OperatorId==request.OperatorId&&x.Active).ToListAsync())old.Active=false;
  await db.SaveChangesAsync();
  var route=new FieldRoute{RequestId=requestId,SourceHash=sourceHash,OperatorId=request.OperatorId,CreatedById=a.Id,Kind=request.Kind,TravelMode=request.TravelMode,StartLatitude=request.StartLatitude,StartLongitude=request.StartLongitude,DistanceMeters=street.DistanceMeters,NetworkHash=street.NetworkHash,PathJson=JsonSerializer.Serialize(street.Path)};
  db.Set<FieldRoute>().Add(route);await db.SaveChangesAsync();
  foreach(var stop in street.Stops){
   var order=selected.Single(o=>o.Id==stop.OrderId);
   var assignment=await db.Set<Assignment>().SingleOrDefaultAsync(x=>x.OrderId==order.Id&&x.End==null);
   if(assignment==null){
    assignment=new Assignment{OrderId=order.Id,OperatorId=request.OperatorId,AssignedById=a.Id,Reason=$"Ruta {route.Id} ({request.Kind})."};db.Set<Assignment>().Add(assignment);order.Status="ASIGNADA";order.CutAuthorizedAt=null;
    db.Set<OrderEvent>().Add(new(){OrderId=order.Id,UserId=a.Id,Action="orden.asignar",Detail=assignment.Reason});await db.SaveChangesAsync();
   }
   Require(assignment.OperatorId==request.OperatorId,"La orden fue reasignada.");
   db.Set<RouteStop>().Add(new(){RouteId=route.Id,OrderId=order.Id,AssignmentId=assignment.Id,Sequence=stop.Sequence,DistanceMeters=stop.DistanceMeters,AccessMeters=stop.AccessMeters});
  }
  db.Set<Notification>().Add(new(){UserId=request.OperatorId,Message=$"Ruta #{route.Id}: {selected.Count} trabajos de {route.Kind}. Actualice la APK para ver el orden por vías."});
  service.Audit(a,"ruta.guardar",route.Id.ToString(),$"Dijkstra · {selected.Count} paradas · {route.DistanceMeters:F0} m.");await db.SaveChangesAsync();return route;
 }

 public async Task<object> Routes(Actor a){
  a.Require("orders.read");var routes=await db.Set<FieldRoute>().AsNoTracking().Where(x=>x.Active&&(a.Can("orders.manage")||x.OperatorId==a.Id)).OrderByDescending(x=>x.Id).Take(50).ToListAsync();
  var ids=routes.Select(x=>x.Id).ToArray();
  var stops=await (from stop in db.Set<RouteStop>() join ass in db.Set<Assignment>() on stop.AssignmentId equals ass.Id join order in service.Orders(a) on stop.OrderId equals order.Id where ids.Contains(stop.RouteId)&&ass.End==null&&ass.OperatorId==(a.Can("orders.manage")?ass.OperatorId:a.Id) orderby stop.Sequence select new{stop.RouteId,stop.Sequence,stop.DistanceMeters,stop.AccessMeters,order}).AsNoTracking().ToListAsync();
  var contractIds=stops.Select(s=>s.order.ContractId).ToArray();var connectionIds=stops.Select(s=>s.order.ConnectionId).ToArray();
  var owners=await (from contract in db.Set<Contract>() join client in db.Set<Client>() on contract.ClientId equals client.Id where contractIds.Contains(contract.Id) select new{contract.Id,client.Name}).ToDictionaryAsync(x=>x.Id,x=>x.Name);
  var connections=await db.Set<Connection>().AsNoTracking().Where(x=>connectionIds.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,x=>x.Code);
  return routes.Select(r=>new{r.Id,r.OperatorId,r.Kind,r.CreatedAt,r.TravelMode,r.StartLatitude,r.StartLongitude,r.DistanceMeters,r.NetworkHash,path=JsonSerializer.Deserialize<double[][]>(r.PathJson),stops=stops.Where(s=>s.RouteId==r.Id).Select(s=>new{s.RouteId,s.Sequence,s.DistanceMeters,s.AccessMeters,s.order,client=s.order.ContractId.HasValue?owners.GetValueOrDefault(s.order.ContractId.Value,"Trabajo de área"):"Trabajo de área",connectionCode=s.order.ConnectionId.HasValue?connections.GetValueOrDefault(s.order.ConnectionId.Value,""):""})}).ToArray();
 }
}
