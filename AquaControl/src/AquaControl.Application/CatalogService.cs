using AquaControl.Domain;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using static AquaControl.Domain.Workflow;
namespace AquaControl.Application;
public record CatalogDefinition(string Key,string Label,Type Type,string Read,string Write,string[] Fields);
public class CatalogService(IData db,AquaService service){
 public static readonly CatalogDefinition[] Definitions=[
  new("clients","Clientes",typeof(Client),"customers.read","customers.write",["Name","Document","Email","Phone","Address","Active"]),
  new("accounts","Cuentas de abonado",typeof(Account),"customers.read","customers.write",["Number","Active"]),
  new("connections","Conexiones",typeof(Connection),"customers.read","customers.write",["Code","FixedCodeId","SectorId","RoadId","Address","Longitude","Latitude"]),
  new("sectors","Sectores",typeof(Sector),"customers.read","customers.write",["Code","Name"]),
  new("meters","Medidores",typeof(Meter),"customers.read","customers.write",["Serial","Model","Active"]),
  new("tariffs","Tarifas (versiones)",typeof(Tariff),"billing.read","billing.write",["Name","Currency","FixedCharge","UnitPrice","Start","Active"]),
  new("bands","Tramos de tarifa",typeof(TariffBand),"billing.read","billing.write",["TariffId","From","To","Price"]),
  new("types","Tipos de trabajo",typeof(WorkType),"orders.read","catalogs.write",["Code","Name","Effect","Active"]),
  new("templates","Actividades de trabajo",typeof(ActivityTemplate),"orders.read","catalogs.write",["WorkTypeId","Sort","Name","Required","EvidenceRequired"]),
  new("materials","Materiales",typeof(Material),"orders.read","catalogs.write",["Code","Name","Unit","Cost","Active"]),
  new("points","Puntos de pago",typeof(PaymentPoint),"portal.read","catalogs.write",["Name","Kind","Institution","Address","Hours","Methods","Longitude","Latitude","Active"]),
  new("policies","Políticas de cobranza",typeof(CollectionPolicy),"orders.manage","catalogs.write",["Threshold","OverdueDays","NoticeDays","Enabled"])
 ];
 public CatalogDefinition Definition(string key)=>Definitions.FirstOrDefault(x=>x.Key==key)??throw new BusinessException("Catálogo desconocido.");
 public async Task<object> List(Actor actor,string key,int page=1,string? search=null){var def=Definition(key);actor.Require(def.Read);return await (Task<object>)GetType().GetMethod(nameof(ListTyped))!.MakeGenericMethod(def.Type).Invoke(this,[page,search])!;}
 public async Task<object> ListTyped<T>(int page,string? search) where T:Entity {
  // Bounded server pagination; search on name/code/number columns available for this catalog.
  var q=db.Set<T>().AsNoTracking();if(!string.IsNullOrWhiteSpace(search)){var field=new[]{"Name","Code","Number","Serial"}.FirstOrDefault(n=>typeof(T).GetProperty(n)!=null);if(field!=null)q=q.Where(x=>EF.Property<string>(x,field).Contains(search));}
  return new{total=await q.CountAsync(),items=await q.OrderByDescending(x=>x.Id).Skip((Math.Clamp(page,1,100000)-1)*50).Take(50).ToListAsync()};
 }
 public async Task<object> Save(Actor actor,string key,int? id,JsonElement body){var def=Definition(key);actor.Require(def.Write);try{return await (Task<object>)GetType().GetMethod(nameof(SaveTyped))!.MakeGenericMethod(def.Type).Invoke(this,[actor,def,id,body])!;}catch(System.Reflection.TargetInvocationException e){throw e.InnerException??e;}}
 public async Task<object> SaveTyped<T>(Actor actor,CatalogDefinition def,int? id,JsonElement body) where T:Entity,new(){
  var entity=id.HasValue?await service.Get<T>(id.Value):new T();if(id.HasValue){Require(body.TryGetProperty("version",out var v),"Versión requerida.");AquaService.Version(entity,v.GetString()??"");}
  if(entity is CollectionPolicy)Require(!id.HasValue,"Las políticas son versiones inmutables: cree otra.");
  if(entity is Tariff&&id.HasValue)Require(!await db.Set<Contract>().AnyAsync(x=>x.TariffId==id),"Tarifa utilizada: cree una nueva versión.");
  if(entity is TariffBand band){var tariffId=id.HasValue?band.TariffId:body.GetProperty("tariffId").GetInt32();Require(!await db.Set<Contract>().AnyAsync(x=>x.TariffId==tariffId),"Tarifa utilizada: sus tramos son inmutables.");}
  foreach(var field in def.Fields){var property=typeof(T).GetProperty(field)!;var jsonName=char.ToLowerInvariant(field[0])+field[1..];if(body.TryGetProperty(jsonName,out var value))property.SetValue(entity,JsonSerializer.Deserialize(value.GetRawText(),property.PropertyType));}
  foreach(var field in new[]{"Name","Code","Number","Serial"}){var p=typeof(T).GetProperty(field);if(p!=null)Require(!string.IsNullOrWhiteSpace(p.GetValue(entity)?.ToString()),$"{field} es obligatorio.");}
  var results=new List<System.ComponentModel.DataAnnotations.ValidationResult>();Require(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(entity,new(entity),results,true),string.Join("; ",results.Select(x=>x.ErrorMessage)));
  if(entity is Connection c){Require(c.FixedCodeId.HasValue,"Seleccione el Código Fijo SIG asociado a la conexión.");Require(Math.Abs(c.Longitude)<=180&&Math.Abs(c.Latitude)<=90,"Coordenadas inválidas.");}
  if(entity is Tariff t)Require(t.FixedCharge>=0&&t.UnitPrice>=0&&t.Currency=="BOB","Importes no negativos y moneda BOB requerida en esta versión.");
  if(entity is TariffBand b)Require(b.From>=0&&(b.To==null||b.To>b.From)&&b.Price>=0,"Tramo inválido.");
  if(entity is WorkType w)Require(w.Effect is "NINGUNO" or "CORTE" or "RECONEXION","Efecto inválido.");
  if(entity is Material m)Require(m.Cost>=0,"Costo inválido.");
  if(entity is CollectionPolicy pcy)Require(pcy.Threshold>=0&&pcy.OverdueDays>=0&&pcy.NoticeDays>=0,"Valores de política inválidos.");
  if(entity is PaymentPoint point)Require(Math.Abs(point.Longitude)<=180&&Math.Abs(point.Latitude)<=90&&new[]{"BANCO","COOPERATIVA","OFICINA","AUTORIZADO"}.Contains(point.Kind),"Punto de pago inválido.");
  if(!id.HasValue)db.Set<T>().Add(entity);service.Audit(actor,"catalogo.guardar",def.Key+":"+(id?.ToString()??"nuevo"));await db.SaveChangesAsync();return entity;
 }
}
