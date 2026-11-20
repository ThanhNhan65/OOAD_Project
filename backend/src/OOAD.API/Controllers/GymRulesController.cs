using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OOAD.Application.DTOs.GymRule;
using OOAD.Application.Services.Interfaces;

namespace OOAD.API.Controllers;

[Route("api/gym-rules")]
public class GymRulesController : ApiControllerBase
{
    private readonly IGymRuleService _gymRuleService;

    public GymRulesController(IGymRuleService gymRuleService)
    {
        _gymRuleService = gymRuleService;
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<GymRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GymRuleDto>>> GetAll(
        [FromQuery] Guid? branchId = null,
        [FromQuery] string? category = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var rules = await _gymRuleService.GetAllAsync(branchId, category, includeInactive, cancellationToken);
        return Ok(rules);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GymRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GymRuleDto>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await _gymRuleService.GetByIdAsync(id, cancellationToken);
        if (rule == null) return NotFound(new { message = "Không tìm thấy nội quy." });
        return Ok(rule);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(GymRuleDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<GymRuleDto>> Create([FromBody] CreateGymRuleDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Content))
        {
            return BadRequest(new { message = "Tiêu đề và nội dung nội quy không được để trống." });
        }

        var created = await _gymRuleService.CreateAsync(dto, GetCurrentUserId(), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(GymRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GymRuleDto>> Update(Guid id, [FromBody] UpdateGymRuleDto dto, CancellationToken cancellationToken = default)
    {
        var updated = await _gymRuleService.UpdateAsync(id, dto, cancellationToken);
        if (updated == null) return NotFound(new { message = "Không tìm thấy nội quy." });
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var ok = await _gymRuleService.DeleteAsync(id, cancellationToken);
        if (!ok) return NotFound(new { message = "Không tìm thấy nội quy." });
        return NoContent();
    }
}