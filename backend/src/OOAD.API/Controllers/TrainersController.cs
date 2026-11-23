using System.Security.Claims;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OOAD.API.Controllers;

public class TrainersController : ApiControllerBase
{
    private readonly ITrainerService _trainerService;
    private readonly IPersonalTrainerService _ptService;

    public TrainersController(ITrainerService trainerService, IPersonalTrainerService ptService)
    {
        _trainerService = trainerService;
        _ptService = ptService;
    }

    /// <summary>
    /// GET /api/trainers
    /// Lấy danh sách tất cả HLV, có thể lọc theo chi nhánh.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<TrainerDto>>> GetAll([FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        var trainers = await _trainerService.GetAllTrainersAsync(branchId, cancellationToken);
        return Ok(trainers);
    }

    /// <summary>
    /// GET /api/trainers/{id}
    /// Lấy thông tin chi tiết một HLV cùng các slot rảnh.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<TrainerDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var trainer = await _trainerService.GetTrainerByIdAsync(id, cancellationToken);
        if (trainer == null) return NotFound(new { message = "Không tìm thấy Huấn luyện viên." });
        return Ok(trainer);
    }

    /// <summary>
    /// GET /api/trainers/me
    /// Lấy hồ sơ của HLV đang đăng nhập.
    /// </summary>
    [HttpGet("me")]
    [Authorize(Roles = "Trainer,Admin")]
    public async Task<ActionResult<TrainerDto>> GetMe(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var trainer = await _trainerService.GetCurrentTrainerProfileAsync(userId.Value, cancellationToken);
        if (trainer == null) return NotFound(new { message = "Không tìm thấy hồ sơ HLV cho tài khoản hiện tại." });
        return Ok(trainer);
    }

    /// <summary>
    /// PUT /api/trainers/me
    /// Cập nhật hồ sơ cá nhân HLV (chuyên môn, kinh nghiệm, bio, sđt, chi nhánh).
    /// </summary>
    [HttpPut("me")]
    [Authorize(Roles = "Trainer,Admin")]
    public async Task<ActionResult<TrainerDto>> UpdateProfile([FromBody] UpdateTrainerProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var updated = await _trainerService.UpdateTrainerProfileAsync(userId.Value, request, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// GET /api/trainers/{id}/availability
    /// Lấy danh sách khung giờ rảnh của HLV theo khoảng thời gian.
    /// </summary>
    [HttpGet("{id:guid}/availability")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<TrainerAvailabilityDto>>> GetAvailability(
        Guid id,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var slots = await _trainerService.GetTrainerAvailabilityAsync(id, from, to, cancellationToken);
        return Ok(slots);
    }

    /// <summary>
    /// POST /api/trainers/availability
    /// HLV thêm khung giờ rảnh mới (có chặn trùng lịch dạy lớp nhóm).
    /// </summary>
    [HttpPost("availability")]
    [Authorize(Roles = "Trainer,Admin")]
    public async Task<ActionResult<TrainerAvailabilityDto>> AddAvailability([FromBody] AvailabilityRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var slot = await _trainerService.AddAvailabilityAsync(userId.Value, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = slot.TrainerId }, slot);
    }

    /// <summary>
    /// DELETE /api/trainers/availability/{id}
    /// HLV xóa khung giờ rảnh chưa có học viên đặt.
    /// </summary>
    [HttpDelete("availability/{id:guid}")]
    [Authorize(Roles = "Trainer,Admin")]
    public async Task<IActionResult> DeleteAvailability(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await _trainerService.DeleteAvailabilityAsync(userId.Value, id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// GET /api/trainers/my-schedule
    /// Lấy toàn bộ lịch làm việc của HLV (bao gồm buổi lớp nhóm và các lịch PT 1-kèm-1).
    /// </summary>
    [HttpGet("my-schedule")]
    [Authorize(Roles = "Trainer,Admin")]
    public async Task<ActionResult<IEnumerable<TrainerScheduleItemDto>>> GetMySchedule(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var schedule = await _trainerService.GetMyScheduleAsync(userId.Value, from, to, cancellationToken);
        return Ok(schedule);
    }

    // ==================== PT 1-kèm-1 ENDPOINTS (HỘI VIÊN & HLV) ====================

    /// <summary>
    /// GET /api/trainers/pt-balance
    /// Hội viên lấy số dư buổi PT còn lại (tặng kèm theo gói + mua gói PT).
    /// </summary>
    [HttpGet("pt-balance")]
    [Authorize]
    public async Task<ActionResult<PtBalanceDto>> GetPtBalance(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var balance = await _ptService.GetPtBalanceAsync(userId.Value, cancellationToken);
        return Ok(balance);
    }

    /// <summary>
    /// POST /api/trainers/pt-bookings
    /// Hội viên gửi yêu cầu đặt lịch PT 1-kèm-1 với HLV theo khung giờ rảnh.
    /// </summary>
    [HttpPost("pt-bookings")]
    [Authorize]
    public async Task<ActionResult<PtBookingDto>> BookPt([FromBody] BookPtRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var booking = await _ptService.BookPtSessionAsync(userId.Value, request, cancellationToken);
        return Ok(booking);
    }

    /// <summary>
    /// GET /api/trainers/pt-bookings/me
    /// Hội viên lấy danh sách các lịch hẹn PT của chính mình.
    /// </summary>
    [HttpGet("pt-bookings/me")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<PtBookingDto>>> GetMyPtBookings(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var list = await _ptService.GetMyPtBookingsAsync(userId.Value, cancellationToken);
        return Ok(list);
    }

    /// <summary>
    /// POST /api/trainers/pt-bookings/{id}/cancel
    /// Hội viên hủy lịch hẹn PT trước thời hạn quy định.
    /// </summary>
    [HttpPost("pt-bookings/{id:guid}/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelPtBooking(Guid id, [FromQuery] string? reason, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await _ptService.CancelPtBookingAsync(userId.Value, id, reason, cancellationToken);
        return Ok(new { message = "Đã hủy lịch hẹn PT thành công và hoàn lại buổi tập vào gói của bạn." });
    }

    /// <summary>
    /// GET /api/trainers/me/pt-requests
    /// HLV lấy danh sách yêu cầu đặt lịch PT (Chờ duyệt, Đã xác nhận, Lịch sử).
    /// </summary>
    [HttpGet("me/pt-requests")]
    [Authorize(Roles = "Trainer,Admin")]
    public async Task<ActionResult<IEnumerable<TrainerPtRequestItemDto>>> GetTrainerPtRequests(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var requests = await _ptService.GetTrainerPtRequestsAsync(userId.Value, cancellationToken);
        return Ok(requests);
    }

    /// <summary>
    /// POST /api/trainers/pt-requests/{id}/respond
    /// HLV xác nhận hoặc từ chối yêu cầu đặt lịch PT.
    /// </summary>
    [HttpPost("pt-requests/{id:guid}/respond")]
    [Authorize(Roles = "Trainer,Admin")]
    public async Task<IActionResult> RespondPtRequest(Guid id, [FromBody] RespondPtRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await _ptService.RespondPtRequestAsync(userId.Value, id, request.Accept, request.Reason, cancellationToken);
        return Ok(new { message = request.Accept ? "Đã xác nhận lịch hẹn PT." : "Đã từ chối lịch hẹn và hoàn trả buổi tập cho hội viên." });
    }

    /// <summary>
    /// POST /api/trainers/pt-requests/{id}/complete
    /// HLV xác nhận hoàn thành buổi tập PT hoặc ghi nhận vắng mặt.
    /// </summary>
    [HttpPost("pt-requests/{id:guid}/complete")]
    [Authorize(Roles = "Trainer,Admin")]
    public async Task<IActionResult> CompletePtSession(Guid id, [FromBody] CompletePtRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await _ptService.CompletePtSessionAsync(userId.Value, id, request.Attended, cancellationToken);
        return Ok(new { message = request.Attended ? "Đã ghi nhận hoàn thành buổi tập PT." : "Đã ghi nhận hội viên vắng mặt." });
    }

    /// <summary>
    /// POST /api/trainers/pt-requests/{id}/cancel
    /// HLV hủy lịch hẹn PT đã xác nhận kèm lý do.
    /// </summary>
    [HttpPost("pt-requests/{id:guid}/cancel")]
    [Authorize(Roles = "Trainer,Admin")]
    public async Task<IActionResult> CancelPtSessionByTrainer(Guid id, [FromBody] RespondPtRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await _ptService.CancelPtByTrainerAsync(userId.Value, id, request.Reason ?? "Huấn luyện viên bận đột xuất", cancellationToken);
        return Ok(new { message = "Đã hủy lịch hẹn PT và hoàn trả buổi tập cho hội viên." });
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value
                    ?? User.FindFirst("id")?.Value;

        if (Guid.TryParse(claim, out var guid))
        {
            return guid;
        }

        return null;
    }
}
