using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;

namespace OOAD.API.Controllers;

/// <summary>
/// Controller quản lý Danh mục Gói hội viên và Gói PT 1-kèm-1.
/// Hỗ trợ cả route /api/membership-plans và /api/membershipplans.
/// </summary>
[Route("api/membership-plans")]
[Route("api/membershipplans")]
public class MembershipPlansController : ApiControllerBase
{
    private readonly IMembershipSubscriptionService _subscriptionService;

    public MembershipPlansController(IMembershipSubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<MembershipPlanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MembershipPlanDto>>> GetAll(
        [FromQuery] string? planType,
        CancellationToken cancellationToken)
    {
        var plans = await _subscriptionService.GetAllPlansAsync(planType, cancellationToken);
        return Ok(plans);
    }

    [HttpGet("pt")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<MembershipPlanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MembershipPlanDto>>> GetPtPlans(CancellationToken cancellationToken)
    {
        var plans = await _subscriptionService.GetAllPlansAsync("PT", cancellationToken);
        return Ok(plans);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MembershipPlanDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MembershipPlanDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var plan = await _subscriptionService.GetPlanByIdAsync(id, cancellationToken);
        if (plan == null) return NotFound(new { message = "Không tìm thấy gói tập." });
        return Ok(plan);
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN,Admin")]
    [ProducesResponseType(typeof(MembershipPlanDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<MembershipPlanDto>> Create(
        [FromBody] CreateUpdateMembershipPlanRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _subscriptionService.CreatePlanAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN,Admin")]
    [ProducesResponseType(typeof(MembershipPlanDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MembershipPlanDto>> Update(
        Guid id,
        [FromBody] CreateUpdateMembershipPlanRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _subscriptionService.UpdatePlanAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _subscriptionService.DeletePlanAsync(id, cancellationToken);
        return NoContent();
    }
}
