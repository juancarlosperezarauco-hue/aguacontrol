using AquaControl.Application;
using AquaControl.Domain;
namespace AquaControl.Infrastructure;
public sealed class PaymentGateway(bool sandbox):IPaymentGateway {
 public Task<Checkout> CreateAsync(PaymentIntent intent,CancellationToken ct){
  if(!sandbox)throw new BusinessException("Proveedor QR/tarjeta no configurado. Configure un adaptador certificado antes de cobrar dinero real.");
  return Task.FromResult(new Checkout("SANDBOX",""));
 }
}
