using OOAD.Application.DTOs;

namespace OOAD.Application.Common.Interfaces;

public interface IPaymentGatewayService
{
    Task<ProcessPaymentRespone> ProcessPaymentAsync(
        ProcessPaymentRequest request,
        CancellationToken ct = default
    );
}
