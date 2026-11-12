using AquaControl.Domain;
using Microsoft.EntityFrameworkCore;
using System.Data;
using static AquaControl.Domain.Workflow;
namespace AquaControl.Application;

public class FieldRules { public double MaxDistanceMeters {get;set;}=150; public double MaxAccuracyMeters {get;set;}=100; public int MaxAgeSeconds {get;set;}=60; }
public record NewVisit(Guid RequestId,int ShiftId,string Outcome,string Observation,string ObservedSerial,decimal? Reading,int? EvidenceId,double Latitude,double Longitude,double Accuracy,DateTime CapturedAt,string Version);
public partial class AquaService {
 public static double DistanceMeters(double aLat,double aLon,double bLat,double bLon){double rad=Math.PI/180;double v=Math.Pow(Math.Sin((bLat-aLat)*rad/2),2)+Math.Cos(aLat*rad)*Math.Cos(bLat*rad)*Math.Pow(Math.Sin((bLon-aLon)*rad/2),2);return 6371008.8*2*Math.Atan2(Math.Sqrt(v),Math.Sqrt(Math.Max(0,1-v)));}
 public async Task<FieldVisit> Visit(Actor a,int id,NewVisit r){
  await RequireFieldOperator(a);using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);var o=await OrderAccess(a,id);await Executor(a,o);
  Require(r.RequestId!=Guid.Empty,"Identificador de visita obligatorio.");
  var prior=await db.Set<FieldVisit>().SingleOrDefaultAsync(x=>x.RequestId==r.RequestId);
  if(prior!=null){Require(prior.OrderId==id&&prior.UserId==a.Id,"Identificador de visita utilizado por otro registro.");await tx.CommitAsync();return prior;}
  Version(o,r.Version);Require(o.Status is "EN_EJECUCION" or "EN_CAMINO","La orden no admite visitas en este estado.");
  Require(r.Outcome is "REALIZADO" or "OBSERVADO","Resultado de visita inválido.");
  Require(r.Outcome!="REALIZADO"||o.Status=="EN_EJECUCION","Inicie el trabajo antes de registrar el resultado realizado.");
  Require(r.Outcome!="OBSERVADO"||!string.IsNullOrWhiteSpace(r.Observation),"Describa por qué no pudo realizarse el trabajo.");
  Require((r.Observation??"").Length<=2000&&(r.ObservedSerial??"").Length<=80,"Observación o número de medidor demasiado largo.");
  var rules=fieldRules??new FieldRules();var now=DateTime.UtcNow;var captured=r.CapturedAt.ToUniversalTime();
  Require(double.IsFinite(r.Latitude)&&Math.Abs(r.Latitude)<=90&&double.IsFinite(r.Longitude)&&Math.Abs(r.Longitude)<=180,"Coordenadas de visita inválidas.");
  Require(double.IsFinite(r.Accuracy)&&r.Accuracy>=0&&r.Accuracy<=rules.MaxAccuracyMeters,$"Obtenga GPS preciso (máximo ±{rules.MaxAccuracyMeters:F0} m).");
  Require(captured>=now.AddSeconds(-rules.MaxAgeSeconds)&&captured<=now.AddSeconds(15),"Obtenga una posición GPS nueva antes de guardar la visita.");
  var shift=await Get<FieldShift>(r.ShiftId);Require(shift.UserId==a.Id&&shift.EndedAt==null,"Inicie su jornada de campo.");
  Require(shift.CapturedAt==captured&&shift.Latitude.HasValue&&Math.Abs(shift.Latitude.Value-r.Latitude)<.0000001&&shift.Longitude.HasValue&&Math.Abs(shift.Longitude.Value-r.Longitude)<.0000001&&shift.Accuracy.HasValue&&Math.Abs(shift.Accuracy.Value-r.Accuracy)<.01,"Envíe esta posición GPS a supervisión antes de registrar la visita.");
  var distance=DistanceMeters(r.Latitude,r.Longitude,o.Latitude,o.Longitude);
  Require(distance<=rules.MaxDistanceMeters,$"Está a {distance:F0} m del destino. Acérquese al servicio (máximo {rules.MaxDistanceMeters:F0} m) para registrar la visita.");
  var installed=await db.Set<MeterInstallation>().SingleOrDefaultAsync(i=>i.ConnectionId==o.ConnectionId&&i.End==null);
  var serial=(r.ObservedSerial??"").Trim();Evidence? evidence=null;
  if(r.EvidenceId.HasValue){evidence=await Get<Evidence>(r.EvidenceId.Value);Require(evidence.OrderId==id&&evidence.UserId==a.Id&&evidence.ContentType is "image/jpeg" or "image/png","Seleccione una foto de esta orden registrada por usted.");}
  if(r.Outcome=="REALIZADO"){
   Require(evidence!=null&&evidence.ConnectionId==o.ConnectionId&&evidence.MeterId==installed?.MeterId,"Adjunte la fotografía del medidor actual antes de registrar el resultado.");
   if(installed!=null){
    var meter=await Get<Meter>(installed.MeterId);Require(string.Equals(meter.Serial,serial,StringComparison.OrdinalIgnoreCase),"El número observado no coincide con el medidor instalado. Registre la diferencia como observación para revisión.");
    Require(r.Reading.HasValue&&r.Reading>=0,"Registre la lectura actual observada del medidor.");
    var previous=await db.Set<Reading>().Where(x=>x.InstallationId==installed.Id).OrderByDescending(x=>x.TakenAt).Select(x=>(decimal?)x.Value).FirstOrDefaultAsync()??installed.InitialReading;
    Require(r.Reading>=previous,"La lectura es menor a la anterior. Registre la anomalía como observación.");
   }else Require(o.Effect is not ("CORTE" or "RECONEXION"),"Registre o corrija el medidor instalado antes de completar un corte o reconexión.");
  }
  var visit=new FieldVisit{RequestId=r.RequestId,OrderId=id,UserId=a.Id,ShiftId=shift.Id,MeterId=installed?.MeterId,EvidenceId=evidence?.Id,ObservedSerial=serial,Reading=r.Reading,Outcome=r.Outcome,Observation=r.Observation??"",Latitude=r.Latitude,Longitude=r.Longitude,Accuracy=r.Accuracy,CapturedAt=captured,DistanceMeters=distance};
  db.Set<FieldVisit>().Add(visit);Event(a,o,"visita.registrar",$"{visit.Outcome} · distancia {distance:F0} m · {visit.Observation}");await db.SaveChangesAsync();await tx.CommitAsync();return visit;
 }
 async Task RequireVisit(Actor a,WorkOrder o,string outcome){
  var assignment=await db.Set<Assignment>().SingleOrDefaultAsync(x=>x.OrderId==o.Id&&x.OperatorId==a.Id&&x.End==null);
  var last=await db.Set<FieldVisit>().Where(v=>v.OrderId==o.Id&&v.UserId==a.Id&&v.ReceivedAt>=(assignment==null?DateTime.UtcNow:assignment.Start)&&v.ReceivedAt>=(o.StartedAt??o.CreatedAt)).OrderByDescending(v=>v.Id).FirstOrDefaultAsync();
  Require(last!=null&&last.Outcome==outcome,$"Registre primero una visita {outcome} con GPS actual para esta orden.");
  if(outcome=="REALIZADO"){
   var meterId=await db.Set<MeterInstallation>().Where(x=>x.ConnectionId==o.ConnectionId&&x.End==null).Select(x=>(int?)x.MeterId).SingleOrDefaultAsync();
   Require(last!.MeterId==meterId,"El medidor cambió. Registre una nueva visita y fotografía.");
  }
 }
 public async Task<object> FieldHistory(Actor a,int operatorId,DateTime date){
  a.Require("field.monitor");var from=DateTime.SpecifyKind(date.Date,DateTimeKind.Utc);var to=from.AddDays(1);
  var shifts=db.Set<FieldShift>().Where(s=>s.UserId==operatorId).Select(s=>s.Id);
  var points=await db.Set<FieldTrack>().AsNoTracking().Where(p=>shifts.Contains(p.ShiftId)&&p.CapturedAt>=from&&p.CapturedAt<to).OrderBy(p=>p.CapturedAt).Take(10000).Select(p=>new{p.Latitude,p.Longitude,p.Accuracy,p.CapturedAt,p.ReceivedAt}).ToListAsync();
  var visits=await db.Set<FieldVisit>().AsNoTracking().Where(p=>p.UserId==operatorId&&p.ReceivedAt>=from&&p.ReceivedAt<to).OrderBy(p=>p.ReceivedAt).ToListAsync();
  return new{points,visits,date=from,timeZone="UTC",limit=10000,truncated=points.Count==10000};
 }
}
