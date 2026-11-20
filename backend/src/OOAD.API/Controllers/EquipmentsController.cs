using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OOAD.Application.DTOs.Equipment;
using OOAD.Application.Services.Interfaces;

namespace OOAD.API.Controllers;

[Route("api/equipments")]
public class EquipmentsController : ApiControllerBase
{
    private readonly IEquipmentService _equipmentService;

    public EquipmentsController(IEquipmentService equipmentService)
    {
        _equipmentService = equipmentService;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<EquipmentDto>>> GetAll(
        [FromQuery] Guid? branchId,
        CancellationToken ct)
    {
        var result = await _equipmentService.GetAllAsync(branchId, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<EquipmentDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _equipmentService.GetByIdAsync(id, ct);
        return result == null ? NotFound(new { message = "Không tìm thấy thiết bị." }) : Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<EquipmentDto>> Create(
        [FromBody] CreateEquipmentDto dto,
        CancellationToken ct)
    {
        var created = await _equipmentService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<ActionResult<EquipmentDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateEquipmentStatusDto dto,
        CancellationToken ct)
    {
        var updated = await _equipmentService.UpdateStatusAsync(id, dto.Status, ct);
        return updated == null ? NotFound(new { message = "Không tìm thấy thiết bị." }) : Ok(updated);
    }

    [HttpGet("reports")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<EquipmentReportDto>>> GetReports(
        [FromQuery] Guid? equipmentId,
        [FromQuery] Guid? branchId,
        CancellationToken ct)
    {
        var result = await _equipmentService.GetReportsAsync(equipmentId, branchId, ct);
        return Ok(result);
    }

    [HttpPost("reports")]
    [Authorize(Roles = "Member")]
    public async Task<ActionResult<EquipmentReportDto>> CreateReport(
        [FromBody] CreateEquipmentReportDto dto,
        CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ." });

        var created = await _equipmentService.CreateReportAsync(userId, dto, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPatch("reports/{id:guid}/status")]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<ActionResult<EquipmentReportDto>> UpdateReportStatus(
        Guid id,
        [FromBody] UpdateReportStatusDto dto,
        CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized(new { message = "Token không hợp lệ." });

        var updated = await _equipmentService.UpdateReportStatusAsync(id, userId, role, dto, ct);
        return updated == null ? NotFound(new { message = "Không tìm thấy phiếu báo hỏng." }) : Ok(updated);
    }
}