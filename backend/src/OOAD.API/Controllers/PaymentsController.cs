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
    public async Task<ActionResult<ProcessPaymentRespone>> ProcessPayment ([FromBody] ProcessPaymentRequest request, CancellationToken ct)
    {
        var result = await _paymentGatewaySercvice.ProcessPaymentAsync(request, ct);
        return Ok(result);
    }
    [HttpGet("vnpay-return")]
    [AllowAnonymous]
    public async Task<IActionResult> VnpayReturn([FromQuery] Dictionary<string, string> queryParams)
    {
        // Lấy CardPaymentStrategy để kiểm tra chữ ký
        if (queryParams.TryGetValue("vnp_ResponseCode", out var responseCode) && responseCode == "00")
        {
            // 00 = Thành công: Bạn có thể cập nhật Payment.Status = Completed & Membership.Status = Active tại đây
            return Ok(new { success = true, message = "Thanh toán VNPay thành công!", data = queryParams });
        }
        return BadRequest(new { success = false, message = "Giao dịch không thành công hoặc bị hủy." });
    }
}
