using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public PaymentsController(IMembershipSubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
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
}
