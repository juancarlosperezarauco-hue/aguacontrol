using AquaControl.Domain;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using static AquaControl.Domain.Workflow;
namespace AquaControl.Application;

public partial class AquaService(IData db,IPaymentGateway gateway) {
 public async Task<T> Get<T>(int id) where T:Entity => await db.Set<T>().FindAsync(id) ?? throw new BusinessException("Registro no encontrado.");
 public async Task<HashSet<string>> Permissions(int userId) {
  var roles=from ur in db.Set<UserRole>() join r in db.Set<Role>() on ur.RoleId equals r.Id where ur.UserId==userId&&r.Active select r.Id;
  var result=await (from rp in db.Set<RolePermission>() join p in db.Set<Permission>() on rp.PermissionId equals p.Id where roles.Contains(rp.RoleId) select p.Code).ToListAsync();
  var overrides=await (from u in db.Set<UserPermission>() join p in db.Set<Permission>() on u.PermissionId equals p.Id where u.UserId==userId select new{p.Code,u.Allow}).ToListAsync();
  var set=result.ToHashSet();foreach(var o in overrides)if(o.Allow)set.Add(o.Code);else set.Remove(o.Code);return set;
 }
 public IQueryable<int> ClientIds(Actor actor)=>db.Set<UserClient>().Where(x=>x.UserId==actor.Id).Select(x=>x.ClientId);
 public IQueryable<Contract> Contracts(Actor actor)=>actor.Can("customers.read")? db.Set<Contract>():db.Set<Contract>().Where(x=>ClientIds(actor).Contains(x.ClientId));
 public async Task AccountAccess(Actor a,int id){ if(a.Can("billing.read"))return;Require(await Contracts(a).AnyAsync(x=>x.AccountId==id),"La cuenta no pertenece a su portal."); }
 public void Audit(Actor a,string action,string resource,string detail="")=>db.Set<Audit>().Add(new Audit{UserId=a.Id,Action=action,Resource=resource,Detail=detail});
 public static void Version(Entity e,string value)=>Require(Convert.ToBase64String(e.Version)==value,"El registro cambió. Recargue antes de continuar.");
 public async Task<object> Dashboard(Actor a) {
  var contracts=Contracts(a).Select(x=>x.Id);var invoices=await db.Set<Invoice>().Where(x=>contracts.Contains(x.ContractId)).ToListAsync();
  decimal due=0;foreach(var i in invoices) due+=await Balance(i.Id);
  var orders=Orders(a);return new { clients=a.Can("customers.read")?await db.Set<Client>().CountAsync():await ClientIds(a).CountAsync(), connections=await Contracts(a).Where(x=>x.End==null).CountAsync(), invoices=invoices.Count, debt=due, orders=await orders.CountAsync(x=>x.Status!="CERRADA"&&x.Status!="CANCELADA"), overdue=invoices.Count(x=>x.DueAt<DateTime.UtcNow), currency="BOB" };
 }
 public async Task<Contract> CreateContract(Actor a,NewContract req){
  a.Require("customers.write");using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  await Get<Account>(req.AccountId);await Get<Client>(req.ClientId);await Get<Connection>(req.ConnectionId);var tariff=await Get<Tariff>(req.TariffId);Require(tariff.Active,"Tarifa inactiva.");
  Require(!await db.Set<Contract>().AnyAsync(x=>x.End==null&&(x.ConnectionId==req.ConnectionId||x.AccountId==req.AccountId)),"La cuenta o conexión ya tiene contrato vigente.");
  var c=new Contract{AccountId=req.AccountId,ClientId=req.ClientId,ConnectionId=req.ConnectionId,TariffId=req.TariffId};db.Set<Contract>().Add(c);Audit(a,"contrato.crear",req.AccountId.ToString());await db.SaveChangesAsync();await tx.CommitAsync();return c;
 }
 public async Task CloseContract(Actor a,int id,string version){a.Require("customers.write");var c=await Get<Contract>(id);Version(c,version);Require(c.End==null,"Contrato ya cerrado.");c.End=DateTime.UtcNow;Audit(a,"contrato.cerrar",id.ToString());await db.SaveChangesAsync();}
 public async Task<MeterInstallation> InstallMeter(Actor a,int connectionId,int meterId,decimal initial,decimal? final){
  a.Require("customers.write");Require(initial>=0,"Lectura inicial inválida.");using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  await Get<Connection>(connectionId);Require((await Get<Meter>(meterId)).Active,"Medidor inactivo.");Require(!await db.Set<MeterInstallation>().AnyAsync(x=>x.MeterId==meterId&&x.End==null),"Medidor ya instalado.");
  var old=await db.Set<MeterInstallation>().SingleOrDefaultAsync(x=>x.ConnectionId==connectionId&&x.End==null);
  if(old!=null){var last=await db.Set<Reading>().Where(x=>x.InstallationId==old.Id).OrderByDescending(x=>x.TakenAt).FirstOrDefaultAsync();Require(final.HasValue&&final>=(last?.Value??old.InitialReading),"Indique lectura final válida del medidor anterior.");old.End=DateTime.UtcNow;old.FinalReading=final;await db.SaveChangesAsync();}
  var m=new MeterInstallation{ConnectionId=connectionId,MeterId=meterId,InitialReading=initial};db.Set<MeterInstallation>().Add(m);Audit(a,"medidor.instalar",connectionId.ToString());await db.SaveChangesAsync();await tx.CommitAsync();return m;
 }
 public async Task<Reading> AddReading(Actor a,NewReading r){
  a.Require("readings.write");Require(DateTime.TryParseExact(r.Period,"yyyy-MM",CultureInfo.InvariantCulture,DateTimeStyles.None,out _),"Período requerido: AAAA-MM.");
  using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);var installation=await Get<MeterInstallation>(r.InstallationId);Require(installation.End==null,"Instalación cerrada.");
  var last=await db.Set<Reading>().Where(x=>x.InstallationId==r.InstallationId).OrderByDescending(x=>x.Period).FirstOrDefaultAsync();Require(last==null||string.CompareOrdinal(r.Period,last.Period)>0,"El período debe ser posterior a la última lectura.");
  var previous=last?.Value??installation.InitialReading;Require(r.Value>=previous,"La lectura no puede disminuir. Registre un cambio de medidor.");Require(!r.Estimated||!string.IsNullOrWhiteSpace(r.Note),"Indique motivo de estimación.");
  var reading=new Reading{InstallationId=r.InstallationId,Period=r.Period,Value=r.Value,PreviousValue=previous,Estimated=r.Estimated,Note=r.Note,UserId=a.Id};db.Set<Reading>().Add(reading);Audit(a,"lectura.registrar",r.InstallationId.ToString());await db.SaveChangesAsync();await tx.CommitAsync();return reading;
 }
 public async Task<Invoice> Bill(Actor a,NewInvoice r){
  r=r with {DueAt=r.DueAt.ToUniversalTime()};a.Require("billing.write");using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  var contract=await Get<Contract>(r.ContractId);var reading=await Get<Reading>(r.ReadingId);var installation=await Get<MeterInstallation>(reading.InstallationId);
  Require(installation.ConnectionId==contract.ConnectionId,"La lectura no pertenece al contrato.");Require(reading.TakenAt>=contract.Start&&(contract.End==null||reading.TakenAt<=contract.End),"La lectura está fuera de la vigencia del contrato.");
  Require(r.DueAt.Date>=DateTime.UtcNow.Date,"Vencimiento inválido.");Require(!await db.Set<Invoice>().AnyAsync(x=>x.ContractId==r.ContractId&&x.Period==reading.Period),"Ya existe factura del período.");
  var tariff=await Get<Tariff>(contract.TariffId);var client=await Get<Client>(contract.ClientId);var consumption=reading.Value-reading.PreviousValue;
  var bands=await db.Set<TariffBand>().Where(x=>x.TariffId==tariff.Id).OrderBy(x=>x.From).ToListAsync();decimal variable=consumption*tariff.UnitPrice;
  if(bands.Count>0){Require(bands[0].From==0&&bands[^1].To==null,"Tramos deben cubrir desde cero hasta infinito.");variable=0;decimal next=0;foreach(var band in bands){Require(band.From==next&&(band.To==null||band.To>band.From),"Tramos superpuestos o con huecos.");variable+=Math.Max(0,Math.Min(consumption,band.To??consumption)-band.From)*band.Price;next=band.To??next;}}
  var invoice=new Invoice{Number="FAC-"+Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(),ContractId=contract.Id,ReadingId=reading.Id,Period=reading.Period,DueAt=r.DueAt,Consumption=consumption,Total=Money(tariff.FixedCharge)+Money(variable),Currency=tariff.Currency,HolderName=client.Name,HolderAddress=client.Address};
  db.Set<Invoice>().Add(invoice);await db.SaveChangesAsync();db.Set<InvoiceLine>().AddRange(new InvoiceLine{InvoiceId=invoice.Id,Description="Cargo fijo · "+tariff.Name,Quantity=1,UnitPrice=tariff.FixedCharge,Amount=Money(tariff.FixedCharge)},new InvoiceLine{InvoiceId=invoice.Id,Description="Consumo m³ · "+tariff.Name,Quantity=consumption,UnitPrice=consumption>0?variable/consumption:0,Amount=Money(variable)});Audit(a,"factura.emitir",invoice.Number);await db.SaveChangesAsync();await tx.CommitAsync();return invoice;
 }
 public async Task<object> GenerateInvoices(Actor a,string period,DateTime dueAt){
  a.Require("billing.write");Require(DateTime.TryParseExact(period,"yyyy-MM",CultureInfo.InvariantCulture,DateTimeStyles.None,out _),"Período requerido: AAAA-MM.");Require(dueAt.Date>=DateTime.UtcNow.Date,"Vencimiento inválido.");
  var candidates=await (from c in db.Set<Contract>() join i in db.Set<MeterInstallation>() on c.ConnectionId equals i.ConnectionId join r in db.Set<Reading>() on i.Id equals r.InstallationId where c.End==null&&i.End==null&&r.Period==period&&!db.Set<Invoice>().Any(f=>f.ContractId==c.Id&&f.Period==period) select new{c.Id,ReadingId=r.Id}).ToListAsync();
  var emitted=new List<int>();var errors=new List<object>();
  foreach(var item in candidates){try{var invoice=await Bill(a,new NewInvoice(item.Id,item.ReadingId,dueAt));emitted.Add(invoice.Id);}catch(BusinessException e){errors.Add(new{contractId=item.Id,error=e.Message});}}
  return new{period,emitted=emitted.Count,invoiceIds=emitted,errors};
 }
 public async Task<decimal> Balance(int invoiceId){
  var i=await Get<Invoice>(invoiceId);var adjustments=await db.Set<InvoiceAdjustment>().Where(x=>x.InvoiceId==invoiceId).SumAsync(x=>(decimal?)x.Amount)??0;
  var reversed=db.Set<PaymentReversal>().Select(x=>x.PaymentId);var paid=await db.Set<PaymentAllocation>().Where(x=>x.InvoiceId==invoiceId&&!reversed.Contains(x.PaymentId)).SumAsync(x=>(decimal?)x.Amount)??0;return Money(i.Total+adjustments-paid);
 }
 public async Task<List<object>> InvoiceList(Actor a){var ids=Contracts(a).Select(x=>x.Id);var list=await db.Set<Invoice>().Where(x=>ids.Contains(x.ContractId)).OrderByDescending(x=>x.Id).Take(500).ToListAsync();var output=new List<object>();foreach(var i in list)output.Add(new{i.Id,i.Number,i.ContractId,i.Period,i.DueAt,i.Total,i.Currency,i.Consumption,i.HolderName,balance=await Balance(i.Id),version=Convert.ToBase64String(i.Version)});return output;}
 public async Task Adjust(Actor a,int invoiceId,decimal amount,string reason){a.Require("billing.adjust");Require(amount!=0&&!string.IsNullOrWhiteSpace(reason),"Importe y motivo obligatorios.");using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);Require(await Balance(invoiceId)+amount>=0,"El ajuste no puede dejar saldo negativo.");db.Set<InvoiceAdjustment>().Add(new(){InvoiceId=invoiceId,Amount=Money(amount),Reason=reason,UserId=a.Id});Audit(a,"factura.ajustar",invoiceId.ToString(),reason);await db.SaveChangesAsync();var i=await Get<Invoice>(invoiceId);var c=await Get<Contract>(i.ContractId);await ReevaluateCuts(c.AccountId,a);await db.SaveChangesAsync();await tx.CommitAsync();}
 public async Task<PaymentIntent> StartPayment(Actor a,NewIntent r,CancellationToken ct){
  await AccountAccess(a,r.AccountId);Require(r.Method is "QR" or "TARJETA","Método inválido.");Require(r.Amount>0&&r.Key.Length is >=8 and <=80,"Importe o clave inválidos.");
  var prior=await db.Set<PaymentIntent>().SingleOrDefaultAsync(x=>x.Key==r.Key);if(prior!=null){Require(prior.AccountId==r.AccountId&&prior.UserId==a.Id&&prior.Amount==Money(r.Amount)&&prior.Method==r.Method,"Clave de pago usada con otra solicitud.");return prior;}
  var ids=Contracts(a).Where(x=>x.AccountId==r.AccountId).Select(x=>x.Id);var invoices=await db.Set<Invoice>().Where(x=>ids.Contains(x.ContractId)).ToListAsync();Require(invoices.Count>0,"No hay facturas.");Require(invoices.Select(x=>x.Currency).Distinct().Count()==1,"Monedas incompatibles.");decimal debt=0;foreach(var i in invoices)debt+=await Balance(i.Id);Require(r.Amount<=debt,"El importe supera la deuda.");
  var intent=new PaymentIntent{AccountId=r.AccountId,UserId=a.Id,Amount=Money(r.Amount),Method=r.Method,Key=r.Key,Currency=invoices[0].Currency};
  var checkout=await gateway.CreateAsync(intent,ct);intent.Provider=checkout.Provider;intent.CheckoutUrl=checkout.Url;db.Set<PaymentIntent>().Add(intent);Audit(a,"pago.iniciar",r.AccountId.ToString());await db.SaveChangesAsync();return intent;
 }
 public async Task<Payment> Confirm(ConfirmPayment r,Actor actor,bool sandbox){
  using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);var intent=await Get<PaymentIntent>(r.IntentId);
  if(sandbox){actor.Require("payments.confirm");Require(intent.Provider=="SANDBOX","No es un pago de pruebas.");}
  var oldEvent=await db.Set<PaymentEvent>().SingleOrDefaultAsync(x=>x.ExternalId==r.EventId);Require(oldEvent==null||oldEvent.IntentId==intent.Id,"Evento asociado a otro intento.");
  Require(intent.Amount==r.Amount&&intent.Currency==r.Currency,"Importe o moneda no coincide.");var existing=await db.Set<Payment>().SingleOrDefaultAsync(x=>x.IntentId==intent.Id);if(existing!=null){await tx.CommitAsync();return existing;}
  Require(intent.Status=="PENDIENTE","Estado de pago inválido.");Require(!string.IsNullOrWhiteSpace(r.EventId)&&r.EventId.Length<=100,"Referencia inválida.");
  var p=new Payment{AccountId=intent.AccountId,IntentId=intent.Id,Reference=intent.Provider+":"+r.EventId,Amount=intent.Amount,Method=intent.Method};db.Set<Payment>().Add(p);await db.SaveChangesAsync();
  var contracts=db.Set<Contract>().Where(x=>x.AccountId==p.AccountId).Select(x=>x.Id);var invoices=await db.Set<Invoice>().Where(x=>contracts.Contains(x.ContractId)&&x.Currency==intent.Currency).OrderBy(x=>x.DueAt).ThenBy(x=>x.Id).ToListAsync();var remaining=p.Amount;
  foreach(var i in invoices){var balance=await Balance(i.Id);var applied=Math.Min(remaining,Math.Max(balance,0));if(applied>0)db.Set<PaymentAllocation>().Add(new(){PaymentId=p.Id,InvoiceId=i.Id,Amount=applied});remaining-=applied;if(remaining==0)break;}
  // Unapplied remainder is retained on Payment and exposed as credit; never discarded.
  intent.Status="CONFIRMADO";db.Set<PaymentEvent>().Add(new(){IntentId=intent.Id,ExternalId=r.EventId});Audit(actor,"pago.confirmar",p.Id.ToString());await db.SaveChangesAsync();await ReevaluateCuts(p.AccountId,actor);await db.SaveChangesAsync();await tx.CommitAsync();return p;
 }
 public async Task Reverse(Actor a,int id,string reference,string reason){a.Require("billing.adjust");Require(!string.IsNullOrWhiteSpace(reference)&&!string.IsNullOrWhiteSpace(reason),"Referencia y motivo requeridos.");using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);await Get<Payment>(id);Require(!await db.Set<PaymentReversal>().AnyAsync(x=>x.PaymentId==id),"Pago ya revertido.");db.Set<PaymentReversal>().Add(new(){PaymentId=id,Reference=reference,Reason=reason,UserId=a.Id});Audit(a,"pago.revertir",id.ToString(),reason);await db.SaveChangesAsync();await tx.CommitAsync();}
}
