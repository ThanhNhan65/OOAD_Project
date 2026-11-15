using System.Security.Claims;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OOAD.API.Controllers;

public class TrainersController : ApiControllerBase
{
    private readonly ITrainerService _trainerService;

    public TrainersController(ITrainerService trainerService)
    {
        _trainerService = trainerService;
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
