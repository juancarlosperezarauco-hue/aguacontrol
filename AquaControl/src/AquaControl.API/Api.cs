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
 public static void Map(WebApplication app){
  var api=app.MapGroup("/api").RequireAuthorization();
  api.MapGet("/dashboard",async(HttpContext c,AquaService s)=>await s.Dashboard(await Actor(c,s)));
  async Task<IReadOnlyDictionary<string,string>> ReadCommercialFiles(HttpRequest request) {
   var form=await request.ReadFormAsync();var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
   foreach(var file in form.Files){using var reader=new StreamReader(file.OpenReadStream());result[Path.GetFileName(file.FileName)]=await reader.ReadToEndAsync();}return result;
  }
  api.MapPost("/commercial-import/validate",async(HttpContext c,AquaService s,CommercialImportService importer)=>{var a=await Actor(c,s);a.Require("customers.write");a.Require("billing.write");return await importer.Validate(await ReadCommercialFiles(c.Request));});
  api.MapPost("/commercial-import/execute",async(HttpContext c,AquaService s,CommercialImportService importer)=>await importer.Execute(await Actor(c,s),await ReadCommercialFiles(c.Request)));
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
  api.MapGet("/map/service-points",async(HttpContext c,AquaService s,AquaDb db)=>{
   var a=await Actor(c,s);a.Require("geo.read");
   var rows=await (from contract in s.Contracts(a)
                   where contract.End==null
                   join client in db.Set<Client>() on contract.ClientId equals client.Id
                   join account in db.Set<Account>() on contract.AccountId equals account.Id
                   join connection in db.Set<Connection>() on contract.ConnectionId equals connection.Id
                   select new {contract,client,account,connection}).AsNoTracking().ToListAsync();
   var connectionIds=rows.Select(x=>x.connection.Id).ToList();
   var installations=await db.Set<MeterInstallation>().AsNoTracking().Where(x=>connectionIds.Contains(x.ConnectionId)&&x.End==null).ToDictionaryAsync(x=>x.ConnectionId);
   var meterIds=installations.Values.Select(x=>x.MeterId).ToList();
   var meters=await db.Set<Meter>().AsNoTracking().Where(x=>meterIds.Contains(x.Id)).ToDictionaryAsync(x=>x.Id);
   return rows.Select(x=>new {x.contract,x.client,x.account,x.connection,
     installation=installations.TryGetValue(x.connection.Id,out var installation)?installation:null,
     meter=installations.TryGetValue(x.connection.Id,out var current)&&meters.TryGetValue(current.MeterId,out var meter)?meter:null});
  });
  api.MapPost("/contracts",async(NewContract r,HttpContext c,AquaService s)=>await s.CreateContract(await Actor(c,s),r));
  api.MapPost("/contracts/{id:int}/close",async(int id,VersionRequest r,HttpContext c,AquaService s)=>{await s.CloseContract(await Actor(c,s),id,r.Version);return Results.Ok();});
  api.MapPost("/installations",async(InstallRequest r,HttpContext c,AquaService s)=>await s.InstallMeter(await Actor(c,s),r.ConnectionId,r.MeterId,r.Initial,r.Final));
  api.MapGet("/readings",async(HttpContext c,AquaService s,AquaDb db)=>{var a=await Actor(c,s);a.Require("billing.read");return await db.Set<Reading>().OrderByDescending(x=>x.Id).Take(500).ToListAsync();});
  api.MapPost("/readings",async(NewReading r,HttpContext c,AquaService s)=>await s.AddReading(await Actor(c,s),r));
  api.MapGet("/invoices",async(HttpContext c,AquaService s)=>await s.InvoiceList(await Actor(c,s)));
  api.MapPost("/invoices",async(NewInvoice r,HttpContext c,AquaService s)=>await s.Bill(await Actor(c,s),r));
  api.MapPost("/invoices/generate",async(GenerateInvoicesRequest r,HttpContext c,AquaService s)=>await s.GenerateInvoices(await Actor(c,s),r.Period,r.DueAt));
  api.MapGet("/invoices/{id:int}",async(int id,HttpContext c,AquaService s,AquaDb db)=>{var a=await Actor(c,s);var invoice=await s.Get<Invoice>(id);Require(await s.Contracts(a).AnyAsync(x=>x.Id==invoice.ContractId),"Factura no accesible.");return new{invoice,balance=await s.Balance(id),lines=await db.Set<InvoiceLine>().Where(x=>x.InvoiceId==id).ToListAsync(),adjustments=await db.Set<InvoiceAdjustment>().Where(x=>x.InvoiceId==id).ToListAsync()};});
  api.MapPost("/invoices/{id:int}/adjust",async(int id,AdjustRequest r,HttpContext c,AquaService s)=>{await s.Adjust(await Actor(c,s),id,r.Amount,r.Reason);return Results.Ok();});
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
  api.MapPost("/notices/generate",async(HttpContext c,AquaService s)=>await s.GenerateNotices(await Actor(c,s)));
  api.MapPost("/notices/{id:int}/resolve",async(int id,ResolveNoticeRequest r,HttpContext c,AquaService s)=>{await s.ResolveNotice(await Actor(c,s),id,r.Reason);return Results.Ok();});
  api.MapGet("/notifications",async(HttpContext c,AquaService s,AquaDb db)=>{var a=await Actor(c,s);return await db.Set<Notification>().Where(x=>x.UserId==a.Id).OrderByDescending(x=>x.Id).Take(100).ToListAsync();});
  api.MapPost("/notifications/{id:int}/read",async(int id,HttpContext c,AquaService s,AquaDb db)=>{var a=await Actor(c,s);var n=await s.Get<Notification>(id);Require(n.UserId==a.Id,"Notificación ajena.");n.ReadAt=DateTime.UtcNow;await db.SaveChangesAsync();return Results.Ok();});
  api.MapGet("/users",async(HttpContext c,AquaService s,AquaDb db)=>{(await Actor(c,s)).Require("security.manage");return new{users=await db.Set<User>().Select(x=>new{x.Id,x.Login,x.Name,x.Active,x.MustChangePassword}).ToListAsync(),roles=await db.Set<Role>().ToListAsync(),assignments=await db.Set<UserRole>().ToListAsync(),permissions=await db.Set<Permission>().ToListAsync(),rolePermissions=await db.Set<RolePermission>().ToListAsync()};});
  api.MapPost("/users",async(UserRequest r,HttpContext c,AquaService s,SecurityService sec)=>{var u=await sec.Create(await Actor(c,s),r.Login,r.Name,r.Password,r.RoleId,r.ClientId);return new{u.Id,u.Login,u.Name};});
  api.MapPost("/users/{id:int}/role",async(int id,RoleRequest r,HttpContext c,AquaService s,SecurityService sec)=>{await sec.SetRole(await Actor(c,s),id,r.RoleId,r.Active);return Results.Ok();});
  api.MapPost("/users/{id:int}/password",async(int id,ResetRequest r,HttpContext c,AquaService s,SecurityService sec)=>{await sec.Reset(await Actor(c,s),id,r.Password);return Results.Ok();});
  api.MapPost("/users/{id:int}/client",async(int id,ClientLinkRequest r,HttpContext c,AquaService s,AquaDb db)=>{var a=await Actor(c,s);a.Require("security.manage");await s.Get<User>(id);await s.Get<Client>(r.ClientId);db.Add(new UserClient{UserId=id,ClientId=r.ClientId});s.Audit(a,"portal.vincular",id.ToString(),"cliente:"+r.ClientId);await db.SaveChangesAsync();return Results.Ok();});
  api.MapGet("/audit",async(HttpContext c,AquaService s,AquaDb db)=>{(await Actor(c,s)).Require("reports.read");return await db.Set<Audit>().OrderByDescending(x=>x.Id).Take(300).ToListAsync();});
  api.MapGet("/reports/summary",async(HttpContext c,AquaService s,AquaDb db)=>{(await Actor(c,s)).Require("reports.read");return new{orders=await db.Set<WorkOrder>().GroupBy(x=>x.Status).Select(g=>new{state=g.Key,count=g.Count()}).ToListAsync(),materials=await db.Set<OrderMaterial>().GroupBy(x=>x.MaterialId).Select(g=>new{materialId=g.Key,quantity=g.Sum(x=>x.Quantity),cost=g.Sum(x=>x.Quantity*x.UnitCost)}).ToListAsync()};});
  api.MapGet("/geo/search",async(string? text,string? layers,int? page,int? pageSize,HttpContext c,AquaService s,GeoService geo)=>await geo.Search(await Actor(c,s),text,layers,page??1,pageSize??25));
  api.MapGet("/geo/places",async(double lat,double lon,int? radius,HttpContext c,AquaService s,GeoService geo)=>await geo.Places(await Actor(c,s),lat,lon,radius??1000,c.RequestAborted));
  api.MapGet("/geo/{layer}/{id:int}",async(string layer,int id,HttpContext c,AquaService s,GeoService geo)=>await geo.Detail(await Actor(c,s),layer,id));
  api.MapGet("/geo/{layer}",async(string layer,double west,double south,double east,double north,HttpContext c,AquaService s,GeoService geo)=>await geo.Layer(await Actor(c,s),layer,west,south,east,north));
  api.MapGet("/geo-summary",async(HttpContext c,AquaService s,GeoService geo)=>await geo.Summary(await Actor(c,s)));
  api.MapGet("/geo-imports",async(HttpContext c,AquaService s,AquaDb db)=>{(await Actor(c,s)).Require("geo.import");return new{imports=await db.Set<GeoImport>().OrderByDescending(x=>x.CreatedAt).Take(100).ToListAsync(),issues=await db.Set<GeoIssue>().OrderByDescending(x=>x.Id).Take(100).Select(x=>new{x.Id,x.ImportId,x.Ordinal,x.Severity,x.Reason}).ToListAsync()};});
  string GeoSource(IWebHostEnvironment env){var folder=Path.GetFullPath(Path.Combine(env.ContentRootPath,"../../../DatosSIG_Reproj"));Require(Directory.Exists(folder),"No se encontró DatosSIG_Reproj en el servidor.");return folder;}
  api.MapPost("/geo-import/upload-preview",async(HttpContext c,AquaService s,GeoService geo,GeoImportCoordinator coordinator,IWebHostEnvironment env)=>{var a=await Actor(c,s);a.Require("geo.import");Require(c.Request.ContentLength is >0 and <=350000000,"La selección SIG supera el límite de 350 MB.");var form=await c.Request.ReadFormAsync(c.RequestAborted);var stems=new[]{"Exp_CodigoFijo_4326","Exp_MapaBase_LOTES_4326","Exp_MapaBase_MZA_4326","Exp_MapaBase_VIAS_4326"};var required=stems.SelectMany(stem=>new[]{".shp",".shx",".dbf",".prj"}.Select(ext=>stem+ext)).ToHashSet(StringComparer.OrdinalIgnoreCase);var allowed=required.Concat(stems.Select(stem=>stem+".cpg")).ToHashSet(StringComparer.OrdinalIgnoreCase);var files=form.Files.ToList();Require(files.Count>0,"Seleccione los archivos SHP y sus componentes.");var names=files.Select(f=>Path.GetFileName(f.FileName)).ToList();Require(names.All(name=>allowed.Contains(name))&&names.Distinct(StringComparer.OrdinalIgnoreCase).Count()==names.Count,"La selección contiene nombres no permitidos o duplicados.");Require(required.All(names.Contains),"Faltan componentes obligatorios .shp, .shx, .dbf o .prj.");Require(files.All(f=>f.Length is >0 and <=100000000),"Cada archivo debe pesar entre 1 byte y 100 MB.");var folder=Path.Combine(env.ContentRootPath,"App_Data","sig-staging",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);try{foreach(var file in files){await using var target=File.Create(Path.Combine(folder,Path.GetFileName(file.FileName)));await file.CopyToAsync(target,c.RequestAborted);}var preview=await geo.Preview(folder);if(!preview.Valid){Directory.Delete(folder,true);return Results.Ok(new{sourceId=(Guid?)null,preview});}var sourceId=coordinator.RegisterStagedSource(folder,a.Id);return Results.Ok(new{sourceId,preview});}catch{try{Directory.Delete(folder,true);}catch{}throw;}});
  api.MapGet("/geo-import/preview",async(HttpContext c,AquaService s,GeoService geo,IWebHostEnvironment env)=>{(await Actor(c,s)).Require("geo.import");return await geo.Preview(GeoSource(env));});
  api.MapGet("/geo-import/job",async(HttpContext c,AquaService s,GeoImportCoordinator coordinator)=>{(await Actor(c,s)).Require("geo.import");return coordinator.Current() is { } job?Results.Ok(job):Results.NoContent();});
  api.MapPost("/geo-import/job",async(GeoImportStartRequest request,HttpContext c,AquaService s,GeoImportCoordinator coordinator,IWebHostEnvironment env)=>{var a=await Actor(c,s);a.Require("geo.import");var source=coordinator.Source(request.SourceId,a.Id,GeoSource(env));return await coordinator.Start(source.Folder,request.Mode,a.Id,source.DeleteAfter);});
  api.MapPost("/geo-import/job/{id:guid}/cancel",async(Guid id,HttpContext c,AquaService s,GeoImportCoordinator coordinator)=>{(await Actor(c,s)).Require("geo.import");return coordinator.Cancel(id);});
  api.MapGet("/geo-import/job/{id:guid}/summary",async(Guid id,string? format,HttpContext c,AquaService s,GeoImportCoordinator coordinator)=>{(await Actor(c,s)).Require("geo.import");var txt=(format??"csv").Equals("txt",StringComparison.OrdinalIgnoreCase);return Results.File(coordinator.Summary(id,txt?"txt":"csv"),txt?"text/plain; charset=utf-8":"text/csv; charset=utf-8",$"AquaControl_MigracionSIG_{id:N}.{(txt?"txt":"csv")}");});
 }
}
record VersionRequest(string Version);record InstallRequest(int ConnectionId,int MeterId,decimal Initial,decimal? Final);record AdjustRequest(decimal Amount,string Reason);record GenerateInvoicesRequest(string Period,DateTime DueAt);record ActivityRequest(bool Done,string Result,string Version);record MaterialRequest(int MaterialId,decimal Quantity);record NoticeRequest(int ContractId);record ResolveNoticeRequest(string Reason);record UserRequest(string Login,string Name,string Password,int RoleId,int? ClientId);record RoleRequest(int RoleId,bool Active);record ResetRequest(string Password);record ClientLinkRequest(int ClientId);record GeoImportStartRequest(string? Mode,Guid? SourceId);
