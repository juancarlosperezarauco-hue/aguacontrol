using AquaControl.Domain;
using Microsoft.EntityFrameworkCore;
using System.Data;
using static AquaControl.Domain.Workflow;
namespace AquaControl.Application;
public partial class AquaService {
 public async Task<object> OperationalDashboard(Actor a){
  a.Require("reports.read");
  var pending=(from n in db.Set<CutNotice>() join ct in db.Set<Contract>() on n.ContractId equals ct.Id where n.Status=="VIGENTE" select ct.ConnectionId);
  var connections=await db.Set<Connection>().AsNoTracking().Select(c=>new{c.Status,Pending=pending.Contains(c.Id)}).ToListAsync();
  var counts=connections.GroupBy(c=>c.Status=="CORTADA"?"CORTADA":c.Status=="BAJA_PARCIAL"?"BAJA_PARCIAL":c.Status is "BAJA_TOTAL" or "INACTIVA"?"BAJA_TOTAL":c.Pending||c.Status=="PENDIENTE_CORTE"?"PENDIENTE_CORTE":c.Status is "PENDIENTE" or "PENDIENTE_INSTALACION" or "EN_INSTALACION"?"PENDIENTE_INSTALACION":"NORMAL").ToDictionary(g=>g.Key,g=>g.Count());
  return new{total=connections.Count,states=new[]{"NORMAL","PENDIENTE_CORTE","CORTADA","BAJA_PARCIAL","BAJA_TOTAL","PENDIENTE_INSTALACION"}.Select(state=>new{state,count=counts.GetValueOrDefault(state)}),orders=await db.Set<WorkOrder>().GroupBy(o=>o.Status).Select(g=>new{state=g.Key,count=g.Count()}).ToListAsync(),routes=await db.Set<FieldRoute>().CountAsync(r=>r.Active),activeShifts=await db.Set<FieldShift>().CountAsync(s=>s.EndedAt==null),visitsToday=await db.Set<FieldVisit>().CountAsync(v=>v.ReceivedAt>=DateTime.UtcNow.Date),serverTime=DateTime.UtcNow};
 }
 public async Task ChangeServiceStatus(Actor a,int id,string status,string reason,string version){
  a.Require("customers.write");Require(status is "BAJA_PARCIAL" or "BAJA_TOTAL" or "ACTIVA","Estado administrativo inválido.");Require(!string.IsNullOrWhiteSpace(reason),"Justifique el cambio de estado.");
  using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);var connection=await Get<Connection>(id);Version(connection,version);
  Require(connection.Status!="CORTADA"&&connection.Status!="PENDIENTE_CORTE","Una conexión cortada o con corte pendiente requiere resolver su trabajo técnico.");
  Require(!await db.Set<WorkOrder>().AnyAsync(o=>o.ConnectionId==id&&o.Status!="CERRADA"&&o.Status!="CANCELADA"&&o.Status!="VERIFICADA"),"Resuelva las órdenes abiertas antes del cambio administrativo.");
  Require(!await (from n in db.Set<CutNotice>() join c in db.Set<Contract>() on n.ContractId equals c.Id where c.ConnectionId==id&&n.Status=="VIGENTE" select n.Id).AnyAsync(),"Resuelva el aviso vigente antes de cambiar el estado administrativo.");
  Audit(a,"servicio.estado",id.ToString(),connection.Status+" → "+status+": "+reason);connection.Status=status;await db.SaveChangesAsync();await tx.CommitAsync();
 }
}
