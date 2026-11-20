using System.Security.Claims;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OOAD.API.Controllers;

/// <summary>
/// Controller quản lý Tham số / Cấu hình hệ thống (theo Bảng tham số Dạng 1 & Dạng 2 - slide 37-38).
/// </summary>
[Route("api/settings")]
public class SystemSettingsController : ApiControllerBase
{
    private readonly ISystemSettingService _settingService;

    public SystemSettingsController(ISystemSettingService settingService)
    {
        _settingService = settingService;
    }

    /// <summary>
    /// Lấy toàn bộ danh sách tham số cấu hình hệ thống (Bảng tham số Dạng 2).
    /// GET /api/settings
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "ADMIN,Admin")]
    [ProducesResponseType(typeof(IEnumerable<SystemSettingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SystemSettingDto>>> GetAll(
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        var settings = await _settingService.GetAllAsync(branchId, cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Lấy cấu hình tổng quan dạng tóm tắt cho giao diện cấu hình Admin (/admin/settings - Dạng 1).
    /// GET /api/settings/summary
    /// </summary>
    [HttpGet("summary")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AppSettingsSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AppSettingsSummaryDto>> GetSummary(
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        var summary = await _settingService.GetSummaryAsync(branchId, cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    /// Cập nhật cấu hình tổng quan hệ thống (dành cho Admin).
    /// PUT /api/settings/summary
    /// </summary>
    [HttpPut("summary")]
    [Authorize(Roles = "ADMIN,Admin")]
    [ProducesResponseType(typeof(AppSettingsSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AppSettingsSummaryDto>> UpdateSummary(
        [FromBody] UpdateAppSettingsSummaryRequest request,
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        var updaterId = GetCurrentUserId();
        var updated = await _settingService.UpdateSummaryAsync(request, updaterId, branchId, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Thêm mới hoặc cập nhật một tham số hệ thống cụ thể theo key.
    /// POST /api/settings
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN,Admin")]
    [ProducesResponseType(typeof(SystemSettingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemSettingDto>> UpsertSetting(
        [FromBody] UpdateSystemSettingRequest request,
        CancellationToken cancellationToken)
    {
        var updaterId = GetCurrentUserId();
        var result = await _settingService.UpsertSettingAsync(request, updaterId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lấy giá trị tham số cấu hình theo key.
    /// GET /api/settings/{key}
    /// </summary>
    [HttpGet("{key}")]
    [Authorize]
    [ProducesResponseType(typeof(SystemSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SystemSettingDto>> GetByKey(
        string key,
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        var setting = await _settingService.GetByKeyAsync(key, branchId, cancellationToken);
        if (setting == null)
        {
            return NotFound(new { message = $"Không tìm thấy tham số với khóa: {key}" });
        }
        return Ok(setting);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(userIdStr, out var id) ? id : null;
    }
}
