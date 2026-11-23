using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;

namespace OOAD.API.Controllers;

/// <summary>
/// Controller xử lý yêu cầu tạm ngưng gói (Freeze): Hội viên gửi đơn, Lễ tân/Admin xét duyệt.
/// </summary>
[Route("api/freeze-requests")]
public class FreezeRequestsController : ApiControllerBase
{
    private readonly IMembershipSubscriptionService _subscriptionService;

    public FreezeRequestsController(IMembershipSubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<FreezeRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FreezeRequestDto>>> GetMyFreezeRequests(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var list = await _subscriptionService.GetMyFreezeRequestsAsync(userId.Value, cancellationToken);
        return Ok(list);
    }

    [HttpGet]
    [Authorize(Roles = "STAFF,ADMIN,Staff,Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<FreezeRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FreezeRequestDto>>> GetAllFreezeRequests(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var list = await _subscriptionService.GetAllFreezeRequestsAsync(status, cancellationToken);
        return Ok(list);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(FreezeRequestDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<FreezeRequestDto>> CreateFreezeRequest(
        [FromBody] CreateFreezeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var created = await _subscriptionService.CreateFreezeRequestAsync(userId.Value, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPost("{id:guid}/handle")]
    [Authorize(Roles = "STAFF,ADMIN,Staff,Admin")]
    [ProducesResponseType(typeof(FreezeRequestDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FreezeRequestDto>> HandleFreezeRequest(
        Guid id,
        [FromBody] HandleFreezeRequest request,
        CancellationToken cancellationToken)
    {
        var staffUserId = GetCurrentUserId();
        if (staffUserId == null) return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var updated = await _subscriptionService.HandleFreezeRequestAsync(staffUserId.Value, id, request, cancellationToken);
        return Ok(updated);
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
