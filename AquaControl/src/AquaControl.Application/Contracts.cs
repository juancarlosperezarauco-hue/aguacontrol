using AquaControl.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
namespace AquaControl.Application;
public interface IData {
 DbSet<T> Set<T>() where T:class;
 Task<int> SaveChangesAsync(CancellationToken cancellationToken=default);
 DatabaseFacade Database {get;}
}
public record Actor(int Id, HashSet<string> Permissions) {
 public bool Can(string permission)=>Permissions.Contains(permission);
 public void Require(string permission){if(!Can(permission))throw new UnauthorizedAccessException("No tiene permiso para esta operación.");}
}
public record Checkout(string Provider,string Url);
public interface IPaymentGateway { Task<Checkout> CreateAsync(PaymentIntent intent,CancellationToken ct); }
public record NewContract(int AccountId,int ClientId,int ConnectionId,int TariffId);
public record NewReading(int InstallationId,string Period,decimal Value,bool Estimated,string Note);
public record NewInvoice(int ContractId,int ReadingId,DateTime DueAt);
public record NewOrder(int WorkTypeId,int? ContractId,int? ConnectionId,int SupervisorId,string Priority,DateTime ScheduledAt,double Longitude,double Latitude,string Instructions,int? NoticeId);
public record AssignOrder(int OperatorId,string Reason,string Version);
public record ChangeOrder(string Status,string Reason,string Version);
public record NewIntent(int AccountId,decimal Amount,string Method,string Key);
public record ConfirmPayment(int IntentId,string EventId,decimal Amount,string Currency);
public record OrderUpdate(DateTime ScheduledAt,string Reason,string Version);
