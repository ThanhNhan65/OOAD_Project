using OOAD.Application.DTOs;

namespace OOAD.Application.Common.Interfaces;

public interface IPaymentGatewayService
{
    Task<ProcessPaymentRespone> ProcessPaymentAsync(
        ProcessPaymentRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken ct = default
    );

    Task<PaymentVerificationResult> VerifyVnpayReturnAsync(
        IDictionary<string, string> queryParams,
        CancellationToken ct = default
    );

    Task<PaymentVerificationResult> VerifyZaloPayReturnAsync(
        string appTransId,
        CancellationToken ct = default
    );

    Task<bool> HandleZaloPayCallbackAsync(
        string data,
        string mac,
        CancellationToken ct = default
    );
}
