using OOAD.Application.DTOs;
namespace OOAD.Application.Common.Interfaces;

public interface IPaymentStrategy
{
        string Method {get;}
    Task<GatewayRespone> ChargeAsync(
        decimal amount,
        bool mockSuccess = true,
        PaymentContext? context = null,
        CancellationToken ct = default 
    );
}