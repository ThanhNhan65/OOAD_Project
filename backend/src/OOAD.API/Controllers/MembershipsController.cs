using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;

namespace OOAD.API.Controllers;

/// <summary>
/// Controller quản lý đăng ký, gia hạn nối tiếp và thanh toán lại gói tập của hội viên.
/// </summary>
[Route("api/memberships")]
public class MembershipsController : ApiControllerBase
{
    private readonly IMembershipSubscriptionService _subscriptionService;

    public MembershipsController(IMembershipSubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<MembershipDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MembershipDto>>> GetMyMemberships(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var list = await _subscriptionService.GetMyMembershipsAsync(userId.Value, cancellationToken);
        return Ok(list);
    }

    [HttpPost("purchase")]
    [Authorize]
    public async Task<ActionResult> Purchase(
        [FromBody] PurchaseMembershipRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(new { message = "Vui lòng đăng nhập để mua gói tập." });

        var (membership, payment) = await _subscriptionService.PurchaseAsync(userId.Value, request, cancellationToken);
        return Ok(new
        {
            membership,
            payment,
            success = request.SimulateSuccess,
            message = request.SimulateSuccess
                ? "Thanh toán thành công – gói đã được kích hoạt!"
                : "Thanh toán thất bại – gói đang chờ thanh toán lại."
        });
    }

    [HttpPost("{id:guid}/retry-payment")]
    [Authorize]
    public async Task<ActionResult> RetryPayment(
        Guid id,
        [FromBody] RetryPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var (membership, payment) = await _subscriptionService.RetryPaymentAsync(userId.Value, id, request, cancellationToken);
        return Ok(new
        {
            membership,
            payment,
            success = request.SimulateSuccess,
            message = request.SimulateSuccess
                ? "Thanh toán thành công – gói đã được kích hoạt!"
                : "Thanh toán thất bại – bạn có thể thử thanh toán lại."
        });
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
