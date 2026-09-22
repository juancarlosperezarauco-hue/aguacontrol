using AquaControl.Application;
using AquaControl.Domain;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations.Schema;
namespace AquaControl.Infrastructure;

[Table("CodigosFijos")] public class FixedCode:Entity { public int? CodF_SQL {get;set;} public string? CodF_SIG {get;set;} public int? CodFijo {get;set;} public string? Nombre {get;set;} public byte Estado {get;set;}=1; public DateTime FechaCambioEstado {get;set;}=DateTime.UtcNow; public int? IdLote {get;set;} public double? Longitud {get;set;} public double? Latitud {get;set;} public Geometry? Geom {get;set;} public int ImportId {get;set;} public int Ordinal {get;set;} public string OriginalJson {get;set;}=""; }
[Table("Lotes")] public class Lot:Entity {public int? IdOrigen {get;set;} public string? NroLote {get;set;} public int? IdManzana {get;set;} public Geometry? Geom {get;set;} public int ImportId {get;set;} public int Ordinal {get;set;} public string OriginalJson {get;set;}=""; }
[Table("Manzanas")] public class Block:Entity {public int? IdOrigen {get;set;} public string? UV_MZA {get;set;} public string? UV {get;set;} public string? MZA {get;set;} public Geometry? Geom {get;set;} public int ImportId {get;set;} public int Ordinal {get;set;} public string OriginalJson {get;set;}=""; }
[Table("Vias")] public class Road:Entity {public int? OBJECTID {get;set;} public string? Nombre {get;set;} public string? TipoVia {get;set;} public string? OSMID {get;set;} public Geometry? Geom {get;set;} public int ImportId {get;set;} public int Ordinal {get;set;} public string OriginalJson {get;set;}=""; }

public class AquaDb(DbContextOptions<AquaDb> options):DbContext(options),IData {
 protected override void OnModelCreating(ModelBuilder b) {
  foreach(var t in typeof(Entity).Assembly.GetTypes().Where(t=>t.IsSubclassOf(typeof(Entity))&&!t.IsAbstract)) b.Entity(t);
  b.Entity<FixedCode>();b.Entity<Lot>();b.Entity<Block>();b.Entity<Road>();
  foreach(var e in b.Model.GetEntityTypes()) {
   foreach(var p in e.GetProperties()) {
    if(p.ClrType==typeof(DateTime)||p.ClrType==typeof(DateTime?))p.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime,DateTime>(v=>v.Kind==DateTimeKind.Unspecified?DateTime.SpecifyKind(v,DateTimeKind.Utc):v.ToUniversalTime(),v=>DateTime.SpecifyKind(v,DateTimeKind.Utc)));
    if(p.ClrType==typeof(decimal)||p.ClrType==typeof(decimal?)){p.SetPrecision(18);p.SetScale(4);}
    if(p.ClrType==typeof(string)&&p.GetMaxLength()==null && p.Name is not ("OriginalJson" or "CheckoutUrl"))p.SetMaxLength(2000);
   }
  }
  b.Entity<User>().Property(x=>x.Id).HasColumnName("IdUsuario");b.Entity<User>().Property(x=>x.Name).HasColumnName("Nombre");b.Entity<User>().Property(x=>x.Iterations).HasColumnName("Iteraciones");b.Entity<User>().Property(x=>x.Active).HasColumnName("Activo");b.Entity<User>().Property(x=>x.CreatedAt).HasColumnName("FechaRegistro");
  b.Entity<Role>().Property(x=>x.Id).HasColumnName("IdRol");b.Entity<Role>().Property(x=>x.Name).HasColumnName("NombreRol");
  b.Entity<FixedCode>().Property(x=>x.Id).HasColumnName("IdCodigo");b.Entity<Lot>().Property(x=>x.Id).HasColumnName("IdLote");b.Entity<Block>().Property(x=>x.Id).HasColumnName("IdManzana");b.Entity<Road>().Property(x=>x.Id).HasColumnName("IdVia");
  b.Entity<WorkOrder>().Property(x=>x.Id).HasColumnName("IdOrden");b.Entity<OrderEvent>().Property(x=>x.Id).HasColumnName("IdEvento");
  b.Entity<FixedCode>().Property(x=>x.Geom).HasColumnType("geometry");b.Entity<Lot>().Property(x=>x.Geom).HasColumnType("geometry");b.Entity<Block>().Property(x=>x.Geom).HasColumnType("geometry");b.Entity<Road>().Property(x=>x.Geom).HasColumnType("geometry");
  void FK<T,P>(string name) where T:class where P:class => b.Entity<T>().HasOne<P>().WithMany().HasForeignKey(name).OnDelete(DeleteBehavior.Restrict);
  void Unique<T>(params string[] fields) where T:class=>b.Entity<T>().HasIndex(fields).IsUnique();
  Unique<User>("Login");Unique<Role>("Name");Unique<UserRole>("UserId","RoleId");Unique<Permission>("Code");Unique<RolePermission>("RoleId","PermissionId");Unique<UserPermission>("UserId","PermissionId");Unique<UserClient>("UserId","ClientId");Unique<UserMenu>("UserId","MenuId");
  FK<UserRole,User>("UserId");FK<UserRole,Role>("RoleId");FK<RolePermission,Role>("RoleId");FK<RolePermission,Permission>("PermissionId");FK<UserPermission,User>("UserId");FK<UserPermission,Permission>("PermissionId");FK<UserClient,User>("UserId");FK<UserClient,Client>("ClientId");FK<Menu,Menu>("ParentId");FK<UserMenu,User>("UserId");FK<UserMenu,Menu>("MenuId");
  Unique<Account>("Number");Unique<Connection>("Code");Unique<Sector>("Code");Unique<Meter>("Serial");Unique<Material>("Code");Unique<WorkType>("Code");Unique<Invoice>("Number");Unique<Invoice>("ContractId","Period");Unique<PaymentIntent>("Key");Unique<Payment>("Reference");Unique<Payment>("IntentId");Unique<PaymentEvent>("ExternalId");Unique<PaymentReversal>("PaymentId");Unique<PaymentReversal>("Reference");Unique<WorkOrder>("Number");Unique<Reading>("InstallationId","Period");Unique<NoticeInvoice>("NoticeId","InvoiceId");
  FK<Connection,FixedCode>("FixedCodeId");FK<Connection,Sector>("SectorId");FK<Connection,Road>("RoadId");FK<FixedCode,Lot>("IdLote");FK<Lot,Block>("IdManzana");
  FK<Contract,Account>("AccountId");FK<Contract,Client>("ClientId");FK<Contract,Connection>("ConnectionId");FK<Contract,Tariff>("TariffId");
  FK<MeterInstallation,Connection>("ConnectionId");FK<MeterInstallation,Meter>("MeterId");FK<Reading,MeterInstallation>("InstallationId");FK<Reading,User>("UserId");FK<TariffBand,Tariff>("TariffId");
  FK<Invoice,Contract>("ContractId");FK<Invoice,Reading>("ReadingId");FK<InvoiceLine,Invoice>("InvoiceId");FK<InvoiceAdjustment,Invoice>("InvoiceId");FK<InvoiceAdjustment,User>("UserId");
  FK<PaymentIntent,Account>("AccountId");FK<PaymentIntent,User>("UserId");FK<Payment,Account>("AccountId");FK<Payment,PaymentIntent>("IntentId");FK<PaymentAllocation,Payment>("PaymentId");FK<PaymentAllocation,Invoice>("InvoiceId");FK<PaymentReversal,Payment>("PaymentId");FK<PaymentReversal,User>("UserId");FK<PaymentEvent,PaymentIntent>("IntentId");
  FK<ActivityTemplate,WorkType>("WorkTypeId");FK<WorkOrder,WorkType>("WorkTypeId");FK<WorkOrder,Connection>("ConnectionId");FK<WorkOrder,Contract>("ContractId");FK<WorkOrder,User>("SupervisorId");FK<OrderActivity,WorkOrder>("OrderId");FK<OrderActivity,User>("UserId");FK<Assignment,WorkOrder>("OrderId");FK<Assignment,User>("OperatorId");FK<Assignment,User>("AssignedById");FK<OrderEvent,WorkOrder>("OrderId");FK<OrderEvent,User>("UserId");
  FK<Evidence,WorkOrder>("OrderId");FK<Evidence,OrderActivity>("ActivityId");FK<Evidence,User>("UserId");FK<OrderMaterial,WorkOrder>("OrderId");FK<OrderMaterial,Material>("MaterialId");FK<OrderMaterial,User>("UserId");FK<CutNotice,Contract>("ContractId");FK<CutNotice,WorkOrder>("OrderId");FK<CutNotice,CollectionPolicy>("PolicyId");FK<NoticeInvoice,CutNotice>("NoticeId");FK<NoticeInvoice,Invoice>("InvoiceId");FK<Notification,User>("UserId");FK<Notification,WorkOrder>("OrderId");FK<Audit,User>("UserId");FK<GeoIssue,GeoImport>("ImportId");
  foreach(var type in new[]{typeof(FixedCode),typeof(Lot),typeof(Block),typeof(Road)}) { b.Entity(type).HasOne(typeof(GeoImport)).WithMany().HasForeignKey("ImportId").OnDelete(DeleteBehavior.Restrict);b.Entity(type).HasIndex("ImportId","Ordinal").IsUnique(); }
  Unique<GeoImport>("Layer","Hash");
  b.Entity<Contract>().HasIndex(x=>x.ConnectionId).IsUnique().HasFilter("[End] IS NULL");b.Entity<Contract>().HasIndex(x=>x.AccountId).IsUnique().HasFilter("[End] IS NULL");b.Entity<Connection>().HasIndex(x=>x.FixedCodeId).IsUnique().HasFilter("[FixedCodeId] IS NOT NULL");
  b.Entity<MeterInstallation>().HasIndex(x=>x.ConnectionId).IsUnique().HasFilter("[End] IS NULL");b.Entity<MeterInstallation>().HasIndex(x=>x.MeterId).IsUnique().HasFilter("[End] IS NULL");b.Entity<Assignment>().HasIndex(x=>x.OrderId).IsUnique().HasFilter("[End] IS NULL");
  b.Entity<CutNotice>().HasIndex(x=>x.ContractId).IsUnique().HasFilter("[Status] = 'VIGENTE'");
  b.Entity<WorkOrder>().HasIndex(x=>new{x.Status,x.ScheduledAt});b.Entity<Invoice>().HasIndex(x=>x.DueAt);b.Entity<Notification>().HasIndex(x=>new{x.UserId,x.ReadAt});
  b.Entity<Connection>().ToTable(t=>t.HasCheckConstraint("CK_Connection_Coordinates","[Longitude] BETWEEN -180 AND 180 AND [Latitude] BETWEEN -90 AND 90"));
  b.Entity<PaymentAllocation>().ToTable(t=>t.HasCheckConstraint("CK_Allocation_Positive","[Amount]>0"));
  b.Entity<Payment>().ToTable(t=>t.HasCheckConstraint("CK_Payment_Positive","[Amount]>0"));
  b.Entity<OrderMaterial>().ToTable(t=>t.HasCheckConstraint("CK_Material_Positive","[Quantity]>0 AND [UnitCost]>=0"));
  b.Entity<Invoice>().ToTable(t=>t.HasCheckConstraint("CK_Invoice_Total","[Total]>=0 AND [Consumption]>=0"));
  b.Entity<WorkOrder>().ToTable(t=>t.HasCheckConstraint("CK_Order_State","[Status] IN ('PENDIENTE','ASIGNADA','EN_CAMINO','EN_EJECUCION','FINALIZADA','VERIFICADA','CERRADA','CANCELADA','NO_REALIZADA','REPROGRAMADA')"));
 }
}
