using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;

namespace OOAD.API.Controllers;

/// <summary>
/// Controller vận hành Check-in / Check-out (Quét thẻ/QR tại Kiosk, nhập thủ công tại quầy lễ tân,
/// tra cứu người đang tập và lịch sử ra/vào).
/// </summary>
[Route("api/checkins")]
public class CheckinsController : ApiControllerBase
{
    private readonly ICheckinService _checkinService;

    public CheckinsController(ICheckinService checkinService)
    {
        _checkinService = checkinService;
    }

    /// <summary>
    /// Quét thẻ / mã QR để check-in tại Kiosk hoặc quầy lễ tân.
    /// POST /api/checkins/card
    /// </summary>
    [HttpPost("card")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CheckinOperationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CheckinOperationResultDto>> CheckInByCard(
        [FromBody] CardCheckinRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _checkinService.CheckInByCardAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Quét thẻ / mã QR để check-out tại Kiosk hoặc quầy lễ tân.
    /// POST /api/checkins/card/checkout
    /// </summary>
    [HttpPost("card/checkout")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CheckinOperationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CheckinOperationResultDto>> CheckOutByCard(
        [FromBody] CardCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _checkinService.CheckOutByCardAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lễ tân check-in thủ công cho hội viên khi quên thẻ / thẻ hỏng (UC-20).
    /// POST /api/checkins/manual
    /// </summary>
    [HttpPost("manual")]
    [Authorize(Roles = "STAFF,ADMIN,Staff,Admin")]
    [ProducesResponseType(typeof(CheckinOperationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CheckinOperationResultDto>> CheckInManual(
        [FromBody] ManualCheckinRequest request,
        CancellationToken cancellationToken)
    {
        var staffUserId = GetCurrentUserId();
        if (staffUserId == null)
            return Unauthorized(new { message = "Vui lòng đăng nhập với tài khoản Lễ tân hoặc Admin." });

        var result = await _checkinService.CheckInManualAsync(request, staffUserId.Value, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lễ tân check-out thủ công theo MemberId.
    /// POST /api/checkins/manual/checkout
    /// </summary>
    [HttpPost("manual/checkout")]
    [Authorize(Roles = "STAFF,ADMIN,Staff,Admin")]
    [ProducesResponseType(typeof(CheckinOperationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CheckinOperationResultDto>> CheckOutManual(
        [FromBody] ManualCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _checkinService.CheckOutManualAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Check-out theo ID lượt check-in.
    /// POST /api/checkins/{id}/checkout
    /// </summary>
    [HttpPost("{id:guid}/checkout")]
    [Authorize(Roles = "STAFF,ADMIN,Staff,Admin")]
    [ProducesResponseType(typeof(CheckinOperationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CheckinOperationResultDto>> CheckOutById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _checkinService.CheckOutByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách hội viên đang có mặt tại chi nhánh (phục vụ Kiosk, Trang chủ lễ tân & Quầy check-in).
    /// GET /api/checkins/active?branchId=
    /// </summary>
    [HttpGet("active")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<CheckinDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CheckinDto>>> GetActiveCheckins(
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        var active = await _checkinService.GetActiveCheckinsAsync(branchId, cancellationToken);
        return Ok(active);
    }

    /// <summary>
    /// Tra cứu lịch sử check-in (Lễ tân / Admin lọc theo chi nhánh, hình thức, ngày, từ khóa).
    /// GET /api/checkins?branchId=&method=&date=&search=
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "STAFF,ADMIN,Staff,Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<CheckinDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CheckinDto>>> GetHistory(
        [FromQuery] Guid? branchId,
        [FromQuery] string? method,
        [FromQuery] DateTime? date,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var history = await _checkinService.GetHistoryAsync(branchId, method, date, search, cancellationToken);
        return Ok(history);
    }

    /// <summary>
    /// Hội viên xem lịch sử ra/vào của chính mình tại trang /member/card.
    /// GET /api/checkins/me
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<CheckinDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CheckinDto>>> GetMyHistory(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "Vui lòng đăng nhập để xem lịch sử check-in." });

        var history = await _checkinService.GetMyHistoryAsync(userId.Value, cancellationToken);
        return Ok(history);
    }

    /// <summary>
    /// Kích hoạt chạy kiểm tra Auto Check-out ngay lập tức (tiện test & demo vấn đáp).
    /// POST /api/checkins/auto-checkout
    /// </summary>
    [HttpPost("auto-checkout")]
    [Authorize(Roles = "STAFF,ADMIN,Staff,Admin")]
    public async Task<ActionResult> RunAutoCheckout(CancellationToken cancellationToken)
    {
        var closedCount = await _checkinService.AutoCheckoutExpiredSessionsAsync(cancellationToken);
        return Ok(new
        {
            closedCount,
            message = $"Đã tự động check-out {closedCount} lượt quá giờ đóng cửa."
        });
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(userIdStr, out var id) ? id : null;
    }
}