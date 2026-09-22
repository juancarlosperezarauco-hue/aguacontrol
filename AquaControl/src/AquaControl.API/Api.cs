using AquaControl.Application;
using AquaControl.Domain;
using AquaControl.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using static AquaControl.Domain.Workflow;
public static class Api {
 public static async Task<Actor> Actor(HttpContext c,AquaService service){var id=int.Parse(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!);return new Actor(id,await service.Permissions(id));}
 public static void Map(WebApplication app,bool sandbox){
  var api=app.MapGroup("/api").RequireAuthorization();
  api.MapGet("/dashboard",async(HttpContext c,AquaService s)=>await s.Dashboard(await Actor(c,s)));
  api.MapGet("/catalogs",async(HttpContext c,AquaService s)=>{var a=await Actor(c,s);return CatalogService.Definitions.Where(d=>a.Can(d.Read)).Select(d=>new{d.Key,d.Label,canWrite=a.Can(d.Write),fields=d.Fields.Select(f=>new{name=char.ToLowerInvariant(f[0])+f[1..],type=d.Type.GetProperty(f)!.PropertyType.Name,nullable=Nullable.GetUnderlyingType(d.Type.GetProperty(f)!.PropertyType)!=null})});});
  api.MapGet("/catalog/{key}",async(string key,int? page,string? search,HttpContext c,AquaService s,CatalogService cats)=>await cats.List(await Actor(c,s),key,page??1,search));
  api.MapPost("/catalog/{key}",async(string key,JsonElement body,HttpContext c,AquaService s,CatalogService cats)=>await cats.Save(await Actor(c,s),key,null,body));
  api.MapPut("/catalog/{key}/{id:int}",async(string key,int id,JsonElement body,HttpContext c,AquaService s,CatalogService cats)=>await cats.Save(await Actor(c,s),key,id,body));
  api.MapGet("/lookup",async(HttpContext c,AquaService s,AquaDb db)=>{var a=await Actor(c,s);var contracts=await s.Contracts(a).OrderByDescending(x=>x.Id).Take(1000).ToListAsync();var clientIds=contracts.Select(x=>x.ClientId).ToList();var connectionIds=contracts.Select(x=>x.ConnectionId).ToList();var accountIds=contracts.Select(x=>x.AccountId).ToList();return new{contracts,
   clients=await db.Set<Client>().Where(x=>a.Can("customers.read")||clientIds.Contains(x.Id)).OrderBy(x=>x.Name).Take(1000).ToListAsync(),
   accounts=await db.Set<Account>().Where(x=>a.Can("customers.read")||accountIds.Contains(x.Id)).OrderBy(x=>x.Number).Take(1000).ToListAsync(),
   connections=await db.Set<Connection>().Where(x=>a.Can("customers.read")||connectionIds.Contains(x.Id)).OrderBy(x=>x.Code).Take(1000).ToListAsync(),
   installations=await db.Set<MeterInstallation>().Where(x=>a.Can("customers.read")||connectionIds.Contains(x.ConnectionId)).OrderByDescending(x=>x.Id).Take(1000).ToListAsync(),
   meters=await db.Set<Meter>().Where(x=>a.Can("customers.read")||db.Set<MeterInstallation>().Any(i=>i.MeterId==x.Id&&connectionIds.Contains(i.ConnectionId))).Take(1000).ToListAsync(),
   tariffs=a.Can("billing.read")?await db.Set<Tariff>().OrderBy(x=>x.Name).ToListAsync():[],
   types=a.Can("orders.read")?await db.Set<WorkType>().OrderBy(x=>x.Name).ToListAsync():[],
   materials=a.Can("orders.read")?await db.Set<Material>().Where(x=>x.Active).OrderBy(x=>x.Name).ToListAsync():[],
   users=a.Can("orders.read")?await db.Set<User>().Where(x=>x.Active).Select(x=>new{x.Id,x.Name}).ToListAsync():null};});
  api.MapPost("/contracts",async(NewContract r,HttpContext c,AquaService s)=>await s.CreateContract(await Actor(c,s),r));
  api.MapPost("/contracts/{id:int}/close",async(int id,VersionRequest r,HttpContext c,AquaService s)=>{await s.CloseContract(await Actor(c,s),id,r.Version);return Results.Ok();});
  api.MapPost("/installations",async(InstallRequest r,HttpContext c,AquaService s)=>await s.InstallMeter(await Actor(c,s),r.ConnectionId,r.MeterId,r.Initial,r.Final));
  api.MapGet("/readings",async(HttpContext c,AquaService s,AquaDb db)=>{var a=await Actor(c,s);a.Require("billing.read");return await db.Set<Reading>().OrderByDescending(x=>x.Id).Take(500).ToListAsync();});
  api.MapPost("/readings",async(NewReading r,HttpContext c,AquaService s)=>await s.AddReading(await Actor(c,s),r));
  api.MapGet("/invoices",async(HttpContext c,AquaService s)=>await s.InvoiceList(await Actor(c,s)));
  api.MapPost("/invoices",async(NewInvoice r,HttpContext c,AquaService s)=>await s.Bill(await Actor(c,s),r));
  api.MapGet("/invoices/{id:int}",async(int id,HttpContext c,AquaService s,AquaDb db)=>{var a=await Actor(c,s);var invoice=await s.Get<Invoice>(id);Require(await s.Contracts(a).AnyAsync(x=>x.Id==invoice.ContractId),"Factura no accesible.");return new{invoice,balance=await s.Balance(id),lines=await db.Set<InvoiceLine>().Where(x=>x.InvoiceId==id).ToListAsync(),adjustments=await db.Set<InvoiceAdjustment>().Where(x=>x.InvoiceId==id).ToListAsync()};});
  api.MapPost("/invoices/{id:int}/adjust",async(int id,AdjustRequest r,HttpContext c,AquaService s)=>{await s.Adjust(await Actor(c,s),id,r.Amount,r.Reason);return Results.Ok();});
  api.MapGet("/payments",async(HttpContext c,AquaService s,AquaDb db)=>{var a=await Actor(c,s);var ids=s.Contracts(a).Select(x=>x.AccountId);return await db.Set<Payment>().Where(x=>ids.Contains(x.AccountId)).OrderByDescending(x=>x.Id).Take(500).Select(x=>new{x.Id,x.AccountId,x.Reference,x.Amount,x.Method,x.ConfirmedAt,reversed=db.Set<PaymentReversal>().Any(r=>r.PaymentId==x.Id),credit=x.Amount-(db.Set<PaymentAllocation>().Where(p=>p.PaymentId==x.Id).Sum(p=>(decimal?)p.Amount)??0)}).ToListAsync();});
  api.MapGet("/payment-intents",async(HttpContext c,AquaService s,AquaDb db)=>{var a=await Actor(c,s);var ids=s.Contracts(a).Select(x=>x.AccountId);return await db.Set<PaymentIntent>().Where(x=>ids.Contains(x.AccountId)).OrderByDescending(x=>x.Id).Take(100).ToListAsync();});
  api.MapPost("/payment-intents",async(NewIntent r,HttpContext c,AquaService s)=>await s.StartPayment(await Actor(c,s),r,c.RequestAborted));
  api.MapPost("/payments/sandbox-confirm",async(ConfirmPayment r,HttpContext c,AquaService s)=>{Require(sandbox,"Sandbox deshabilitado.");return await s.Confirm(r,await Actor(c,s),true);});
  api.MapGet("/orders",async(HttpContext c,AquaService s)=>await s.Orders(await Actor(c,s)).OrderByDescending(x=>x.Id).Take(500).ToListAsync());
  api.MapGet("/orders/{id:int}",async(int id,HttpContext c,AquaService s)=>await s.OrderDetail(await Actor(c,s),id));
  api.MapPost("/orders",async(NewOrder r,HttpContext c,AquaService s)=>await s.CreateOrder(await Actor(c,s),r));
  api.MapPost("/orders/{id:int}/assign",async(int id,AssignOrder r,HttpContext c,AquaService s)=>{await s.Assign(await Actor(c,s),id,r);return Results.Ok();});
  api.MapPost("/orders/{id:int}/transition",async(int id,ChangeOrder r,HttpContext c,AquaService s)=>{await s.Transition(await Actor(c,s),id,r);return Results.Ok();});
  api.MapPost("/orders/{id:int}/schedule",async(int id,OrderUpdate r,HttpContext c,AquaService s)=>{await s.Reschedule(await Actor(c,s),id,r);return Results.Ok();});
  api.MapPost("/orders/{id:int}/activities/{aid:int}",async(int id,int aid,ActivityRequest r,HttpContext c,AquaService s)=>{await s.Activity(await Actor(c,s),id,aid,r.Done,r.Result,r.Version);return Results.Ok();});
  api.MapPost("/orders/{id:int}/authorize-cut",async(int id,VersionRequest r,HttpContext c,AquaService s)=>{await s.AuthorizeCut(await Actor(c,s),id,r.Version);return Results.Ok();});
  api.MapPost("/orders/{id:int}/effect",async(int id,VersionRequest r,HttpContext c,AquaService s)=>{await s.RecordEffect(await Actor(c,s),id,r.Version);return Results.Ok();});
  api.MapPost("/orders/{id:int}/materials",async(int id,MaterialRequest r,HttpContext c,AquaService s)=>{await s.AddMaterial(await Actor(c,s),id,r.MaterialId,r.Quantity);return Results.Ok();});
  api.MapPost("/orders/{id:int}/evidence",async(int id,HttpContext c,AquaService s,IWebHostEnvironment env)=>{
   Require(c.Request.ContentLength is >0 and <11000000,"Archivo demasiado grande (máximo 10 MB).");var form=await c.Request.ReadFormAsync();var f=form.Files.GetFile("file")??throw new BusinessException("Seleccione archivo.");Require(f.Length is >0 and <=10000000,"Tamaño inválido.");using var memory=new MemoryStream();await f.CopyToAsync(memory);var bytes=memory.ToArray();string ext,ct;
   if(bytes.Length>8&&bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})){ext=".png";ct="image/png";}else if(bytes.Length>3&&bytes[0]==255&&bytes[1]==216&&bytes[2]==255){ext=".jpg";ct="image/jpeg";}else if(bytes.Length>4&&System.Text.Encoding.ASCII.GetString(bytes,0,4)=="%PDF"){ext=".pdf";ct="application/pdf";}else throw new BusinessException("Solo PNG, JPEG y PDF válidos.");
   var key=Guid.NewGuid().ToString("N")+ext;var folder=Path.Combine(env.ContentRootPath,"App_Data","evidence");Directory.CreateDirectory(folder);var path=Path.Combine(folder,key);await File.WriteAllBytesAsync(path,bytes);
   try{await s.AddEvidence(await Actor(c,s),new Evidence{OrderId=id,ActivityId=int.TryParse(form["activityId"],out var aid)?aid:null,FileName=Path.GetFileName(f.FileName),StorageKey=key,ContentType=ct,Hash=Convert.ToHexString(SHA256.HashData(bytes)),Size=bytes.Length});}catch{File.Delete(path);throw;}return Results.Ok();
  });
  api.MapGet("/evidence/{id:int}",async(int id,HttpContext c,AquaService s,IWebHostEnvironment env)=>{var e=await s.Get<Evidence>(id);await s.OrderAccess(await Actor(c,s),e.OrderId);return Results.File(Path.Combine(env.ContentRootPath,"App_Data","evidence",e.StorageKey),e.ContentType,e.FileName);});
  api.MapGet("/notices",async(HttpContext c,AquaService s,AquaDb db)=>{var ids=s.Contracts(await Actor(c,s)).Select(x=>x.Id);return await db.Set<CutNotice>().Where(x=>ids.Contains(x.ContractId)).OrderByDescending(x=>x.Id).Take(500).ToListAsync();});
  api.MapPost("/notices",async(NoticeRequest r,HttpContext c,AquaService s)=>await s.CreateNotice(await Actor(c,s),r.ContractId));
  api.MapGet("/notifications",async(HttpContext c,AquaService s,AquaDb db)=>{var a=await Actor(c,s);return await db.Set<Notification>().Where(x=>x.UserId==a.Id).OrderByDescending(x=>x.Id).Take(100).ToListAsync();});
  api.MapGet("/payment-points",async(HttpContext c,AquaService s,AquaDb db)=>{(await Actor(c,s)).Require("portal.read");return await db.Set<PaymentPoint>().Where(x=>x.Active).OrderBy(x=>x.Name).Take(1000).ToListAsync();});
  api.MapPost("/notifications/{id:int}/read",async(int id,HttpContext c,AquaService s,AquaDb db)=>{var a=await Actor(c,s);var n=await s.Get<Notification>(id);Require(n.UserId==a.Id,"Notificación ajena.");n.ReadAt=DateTime.UtcNow;await db.SaveChangesAsync();return Results.Ok();});
  api.MapGet("/users",async(HttpContext c,AquaService s,AquaDb db)=>{(await Actor(c,s)).Require("security.manage");return new{users=await db.Set<User>().Select(x=>new{x.Id,x.Login,x.Name,x.Active,x.MustChangePassword}).ToListAsync(),roles=await db.Set<Role>().ToListAsync(),assignments=await db.Set<UserRole>().ToListAsync(),permissions=await db.Set<Permission>().ToListAsync(),rolePermissions=await db.Set<RolePermission>().ToListAsync()};});
  api.MapPost("/users",async(UserRequest r,HttpContext c,AquaService s,SecurityService sec)=>{var u=await sec.Create(await Actor(c,s),r.Login,r.Name,r.Password,r.RoleId,r.ClientId);return new{u.Id,u.Login,u.Name};});
  api.MapPost("/users/{id:int}/role",async(int id,RoleRequest r,HttpContext c,AquaService s,SecurityService sec)=>{await sec.SetRole(await Actor(c,s),id,r.RoleId,r.Active);return Results.Ok();});
  api.MapPost("/users/{id:int}/password",async(int id,ResetRequest r,HttpContext c,AquaService s,SecurityService sec)=>{await sec.Reset(await Actor(c,s),id,r.Password);return Results.Ok();});
  api.MapPost("/users/{id:int}/client",async(int id,ClientLinkRequest r,HttpContext c,AquaService s,AquaDb db)=>{var a=await Actor(c,s);a.Require("security.manage");await s.Get<User>(id);await s.Get<Client>(r.ClientId);db.Add(new UserClient{UserId=id,ClientId=r.ClientId});s.Audit(a,"portal.vincular",id.ToString(),"cliente:"+r.ClientId);await db.SaveChangesAsync();return Results.Ok();});
  api.MapGet("/audit",async(HttpContext c,AquaService s,AquaDb db)=>{(await Actor(c,s)).Require("reports.read");return await db.Set<Audit>().OrderByDescending(x=>x.Id).Take(300).ToListAsync();});
  api.MapGet("/reports/summary",async(HttpContext c,AquaService s,AquaDb db)=>{(await Actor(c,s)).Require("reports.read");return new{orders=await db.Set<WorkOrder>().GroupBy(x=>x.Status).Select(g=>new{state=g.Key,count=g.Count()}).ToListAsync(),payments=await db.Set<Payment>().Where(p=>!db.Set<PaymentReversal>().Any(r=>r.PaymentId==p.Id)).GroupBy(x=>x.Method).Select(g=>new{method=g.Key,total=g.Sum(x=>x.Amount)}).ToListAsync(),materials=await db.Set<OrderMaterial>().GroupBy(x=>x.MaterialId).Select(g=>new{materialId=g.Key,quantity=g.Sum(x=>x.Quantity),cost=g.Sum(x=>x.Quantity*x.UnitCost)}).ToListAsync()};});
  api.MapGet("/geo/{layer}",async(string layer,double west,double south,double east,double north,HttpContext c,AquaService s,GeoService geo)=>await geo.Layer(await Actor(c,s),layer,west,south,east,north));
  api.MapGet("/geo-summary",async(HttpContext c,AquaService s,GeoService geo)=>await geo.Summary(await Actor(c,s)));
  api.MapGet("/geo-imports",async(HttpContext c,AquaService s,AquaDb db)=>{(await Actor(c,s)).Require("geo.import");return new{imports=await db.Set<GeoImport>().ToListAsync(),issues=await db.Set<GeoIssue>().OrderByDescending(x=>x.Id).Take(100).Select(x=>new{x.Id,x.ImportId,x.Ordinal,x.Reason}).ToListAsync()};});
 }
}
record VersionRequest(string Version);record InstallRequest(int ConnectionId,int MeterId,decimal Initial,decimal? Final);record AdjustRequest(decimal Amount,string Reason);record ActivityRequest(bool Done,string Result,string Version);record MaterialRequest(int MaterialId,decimal Quantity);record NoticeRequest(int ContractId);record UserRequest(string Login,string Name,string Password,int RoleId,int? ClientId);record RoleRequest(int RoleId,bool Active);record ResetRequest(string Password);record ClientLinkRequest(int ClientId);
