using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;

namespace OOAD.API.Controllers;

/// <summary>
/// Controller cung cấp dữ liệu tổng quan vận hành cho Trang chủ Lễ tân (/staff).
/// </summary>
[Route("api/staff")]
public class StaffController : ApiControllerBase
{
    private readonly ICheckinService _checkinService;

    public StaffController(ICheckinService checkinService)
    {
        _checkinService = checkinService;
    }

    /// <summary>
    /// Lấy thống kê trong ngày + danh sách hội viên đang tập tại chi nhánh của lễ tân.
    /// GET /api/staff/dashboard?branchId=
    /// </summary>
    [HttpGet("dashboard")]
    [Authorize(Roles = "STAFF,ADMIN,Staff,Admin")]
    [ProducesResponseType(typeof(StaffDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StaffDashboardDto>> GetDashboard(
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized(new { message = "Vui lòng đăng nhập với tài khoản Lễ tân hoặc Admin." });

        var dashboard = await _checkinService.GetStaffDashboardAsync(userId, branchId, cancellationToken);
        return Ok(dashboard);
    }
}