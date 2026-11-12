using AquaControl.Domain;
using Microsoft.EntityFrameworkCore;
using System.Data;
using static AquaControl.Domain.Workflow;
namespace AquaControl.Application;

public record FieldPosition(int ShiftId,double Latitude,double Longitude,double Accuracy,DateTime CapturedAt);
public partial class AquaService {
 public async Task RequireFieldOperator(Actor a){
  a.Require("field.share");a.Require("orders.execute");
  Require(await (from ur in db.Set<UserRole>() join r in db.Set<Role>() on ur.RoleId equals r.Id where ur.UserId==a.Id&&r.Active&&(r.Name==RolePolicy.Reader||r.Name==RolePolicy.Technician) select ur.Id).AnyAsync(),"La jornada de campo es exclusiva de los dos perfiles de operario.");
 }
 public async Task<FieldShift> StartShift(Actor a){
  await RequireFieldOperator(a);using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  var shift=await db.Set<FieldShift>().SingleOrDefaultAsync(x=>x.UserId==a.Id&&x.EndedAt==null);
  if(shift==null){shift=new FieldShift{UserId=a.Id};db.Set<FieldShift>().Add(shift);Audit(a,"jornada.iniciar","ubicacion","Seguimiento iniciado por el operario.");await db.SaveChangesAsync();}
  await tx.CommitAsync();return shift;
 }
 public async Task PublishPosition(Actor a,FieldPosition p){
  await RequireFieldOperator(a);using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  Require(double.IsFinite(p.Latitude)&&Math.Abs(p.Latitude)<=90&&double.IsFinite(p.Longitude)&&Math.Abs(p.Longitude)<=180,"Coordenadas inválidas.");
  Require(double.IsFinite(p.Accuracy)&&p.Accuracy is >=0 and <=1000,"Precisión GPS inválida (máximo 1000 m).");
  var captured=p.CapturedAt.ToUniversalTime();Require(captured>=DateTime.UtcNow.AddMinutes(-2)&&captured<=DateTime.UtcNow.AddSeconds(30),"Ubicación antigua o fecha futura; obtenga una posición GPS nueva.");
  var shift=await Get<FieldShift>(p.ShiftId);Require(shift.UserId==a.Id&&shift.EndedAt==null,"Jornada ajena o finalizada.");
  Require(shift.CapturedAt==null||captured>shift.CapturedAt,"Posición fuera de orden.");
  shift.Latitude=p.Latitude;shift.Longitude=p.Longitude;shift.Accuracy=p.Accuracy;shift.CapturedAt=captured;shift.ReceivedAt=DateTime.UtcNow;
  var previous=await db.Set<FieldTrack>().Where(x=>x.ShiftId==shift.Id).OrderByDescending(x=>x.CapturedAt).FirstOrDefaultAsync();
  if(previous==null||captured>=previous.CapturedAt.AddSeconds(10))db.Set<FieldTrack>().Add(new(){ShiftId=shift.Id,Latitude=p.Latitude,Longitude=p.Longitude,Accuracy=p.Accuracy,CapturedAt=captured});
  await db.SaveChangesAsync();await tx.CommitAsync();
 }
 public async Task EndShift(Actor a,int shiftId){
  await RequireFieldOperator(a);using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  var shift=await Get<FieldShift>(shiftId);Require(shift.UserId==a.Id,"Jornada ajena.");
  if(shift.EndedAt==null){shift.EndedAt=DateTime.UtcNow;Audit(a,"jornada.finalizar",shift.Id.ToString());await db.SaveChangesAsync();}
  await tx.CommitAsync();
 }
 public async Task<object> FieldPositions(Actor a){
  a.Require("field.monitor");
  var users=await (from u in db.Set<User>() join ur in db.Set<UserRole>() on u.Id equals ur.UserId join r in db.Set<Role>() on ur.RoleId equals r.Id where u.Active&&r.Active&&(r.Name==RolePolicy.Reader||r.Name==RolePolicy.Technician) orderby u.Name select new{u.Id,u.Name,Role=r.Name}).ToListAsync();
  var shifts=await db.Set<FieldShift>().Where(x=>x.EndedAt==null).ToDictionaryAsync(x=>x.UserId);
  var now=DateTime.UtcNow;
  return new{serverTime=now,refreshSeconds=10,operators=users.Select(u=>{
   shifts.TryGetValue(u.Id,out var shift);var fresh=shift?.ReceivedAt>=now.AddSeconds(-90)&&shift?.CapturedAt>=now.AddSeconds(-120);
   return new{u.Id,u.Name,u.Role,shiftId=shift?.Id,status=shift==null?"FUERA_DE_JORNADA":fresh?"EN_LINEA":shift.ReceivedAt==null?"ESPERANDO_GPS":"SIN_SEÑAL",latitude=shift?.Latitude,longitude=shift?.Longitude,accuracy=shift?.Accuracy,capturedAt=shift?.CapturedAt,receivedAt=shift?.ReceivedAt,isLive=fresh};
  }).ToList()};
 }
 public async Task<object> MobileContext(Actor a,int id){
  await RequireFieldOperator(a);var order=await OrderAccess(a,id);var type=await Get<WorkType>(order.WorkTypeId);
  var connection=order.ConnectionId.HasValue?await Get<Connection>(order.ConnectionId.Value):null;
  var contract=order.ContractId.HasValue?await Get<Contract>(order.ContractId.Value):null;
  var client=contract!=null?await Get<Client>(contract.ClientId):null;
  var installation=connection!=null?await db.Set<MeterInstallation>().SingleOrDefaultAsync(i=>i.ConnectionId==connection.Id&&i.End==null):null;
  var meter=installation!=null?await Get<Meter>(installation.MeterId):null;
  var last=installation!=null?await db.Set<Reading>().Where(r=>r.InstallationId==installation.Id).OrderByDescending(r=>r.Period).FirstOrDefaultAsync():null;
  var fixedCode=connection?.FixedCodeId is int codeId&&geo!=null?await geo.FixedCodeLabel(codeId):null;
  return new{order,fixedCode,type=new{type.Code,type.Name},client=client==null?null:new{client.Name},connection=connection==null?null:new{connection.Id,connection.Code,connection.Address,connection.Status},installationId=installation?.Id,meter=meter==null?null:new{meter.Id,meter.Serial},previousReading=last?.Value??installation?.InitialReading,lastPeriod=last?.Period,readings=installation==null?[]:await db.Set<Reading>().Where(r=>r.InstallationId==installation.Id&&r.UserId==a.Id).OrderByDescending(r=>r.Id).Take(12).Select(r=>new{r.Id,r.Period,r.Value}).ToListAsync(),evidence=await db.Set<Evidence>().Where(e=>e.OrderId==id).Select(e=>new{e.Id,e.FileName,e.ContentType,e.MeterSerial,e.ConnectionId,e.MeterId,e.ActivityId,e.CreatedAt}).ToListAsync(),visits=await db.Set<FieldVisit>().Where(v=>v.OrderId==id).OrderByDescending(v=>v.Id).Take(10).ToListAsync(),visitRules=fieldRules??new FieldRules(),availableMeters=a.Can("meters.install")?await db.Set<Meter>().Where(m=>m.Active&&!db.Set<MeterInstallation>().Any(i=>i.MeterId==m.Id&&i.End==null)).OrderBy(m=>m.Serial).Take(200).Select(m=>new{m.Id,m.Serial}).ToListAsync():[]};
 }
}
