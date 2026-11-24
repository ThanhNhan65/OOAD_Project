using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OOAD.Application.Common.Interfaces;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;

namespace OOAD.API.Controllers;

/// <summary>
/// Controller tra cứu lịch sử giao dịch thanh toán của Hội viên và toàn hệ thống.
/// </summary>
[Route("api/payments")]
public class PaymentsController : ApiControllerBase
{
    private readonly IMembershipSubscriptionService _subscriptionService;
    private readonly IPaymentGatewayService _paymentGatewaySercvice;

    public PaymentsController(IMembershipSubscriptionService subscriptionService, IPaymentGatewayService paymentGatewayService)
    {
        _subscriptionService = subscriptionService;
        _paymentGatewaySercvice = paymentGatewayService;
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> GetMyPayments(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var list = await _subscriptionService.GetMyPaymentsAsync(userId.Value, cancellationToken);
        return Ok(list);
    }

    [HttpGet]
    [Authorize(Roles = "STAFF,ADMIN,Staff,Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> GetAllPayments(
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        var list = await _subscriptionService.GetAllPaymentsAsync(branchId, cancellationToken);
        return Ok(list);
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    [HttpPost("process")]
    [Authorize]
    public async Task<ActionResult<ProcessPaymentRespone>> ProcessPayment([FromBody] ProcessPaymentRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        var result = await _paymentGatewaySercvice.ProcessPaymentAsync(request, userId.Value, role, ct);
        return Ok(result);
    }
    [HttpGet("vnpay-return")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PaymentVerificationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentVerificationResult>> VnpayReturn(
        [FromQuery] Dictionary<string, string> queryParams,
        CancellationToken ct)
    {
        var result = await _paymentGatewaySercvice.VerifyVnpayReturnAsync(queryParams, ct);
        return Ok(result);
    }

    [HttpGet("zalopay-return")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PaymentVerificationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentVerificationResult>> ZaloPayReturn(
        [FromQuery] string apptransid,
        CancellationToken ct)
    {
        var result = await _paymentGatewaySercvice.VerifyZaloPayReturnAsync(apptransid, ct);
        return Ok(result);
    }

    [HttpPost("zalopay-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> ZaloPayCallback(
        [FromBody] ZaloPayCallbackRequest request,
        CancellationToken ct)
    {
        var ok = await _paymentGatewaySercvice.HandleZaloPayCallbackAsync(request.Data, request.Mac, ct);
        if (ok)
        {
            return Ok(new { return_code = 1, return_message = "success" });
        }

        return Ok(new { return_code = 0, return_message = "mac not equal or order not found" });
    }
}
