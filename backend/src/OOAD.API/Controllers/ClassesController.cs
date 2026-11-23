using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;

namespace OOAD.API.Controllers;

/// <summary>
/// Controller quản lý Lớp học nhóm (GymClass), Buổi học cụ thể (ClassSession),
/// Đổi/Hủy lịch và Điểm danh đối chiếu Check-in (UC-11, UC-14, UC-15).
/// </summary>
[Route("api/classes")]
public class ClassesController : ApiControllerBase
{
    private readonly IGymClassService _gymClassService;
    private readonly IClassBookingService _classBookingService;

    public ClassesController(IGymClassService gymClassService, IClassBookingService classBookingService)
    {
        _gymClassService = gymClassService;
        _classBookingService = classBookingService;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<GymClassDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GymClassDto>>> GetAll(
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? trainerId,
        CancellationToken cancellationToken)
    {
        var classes = await _gymClassService.GetAllClassesAsync(branchId, trainerId, cancellationToken);
        return Ok(classes);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GymClassDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GymClassDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var gymClass = await _gymClassService.GetClassByIdAsync(id, cancellationToken);
        return Ok(gymClass);
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN,Admin")]
    [ProducesResponseType(typeof(GymClassDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<GymClassDto>> Create(
        [FromBody] CreateGymClassRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _gymClassService.CreateClassAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN,Admin")]
    [ProducesResponseType(typeof(GymClassDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GymClassDto>> Update(
        Guid id,
        [FromBody] UpdateGymClassRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _gymClassService.UpdateClassAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _gymClassService.DeleteClassAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/generate-sessions")]
    [Authorize(Roles = "ADMIN,Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<ClassSessionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassSessionDto>>> GenerateSessions(
        Guid id,
        [FromBody] GenerateSessionsRequest? request,
        CancellationToken cancellationToken)
    {
        var sessions = await _gymClassService.GenerateSessionsAsync(
            id, request ?? new GenerateSessionsRequest(), cancellationToken);
        return Ok(sessions);
    }

    [HttpGet("sessions")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<ClassSessionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassSessionDto>>> GetSessions(
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? classId,
        [FromQuery] Guid? trainerId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var sessions = await _gymClassService.GetSessionsAsync(
            branchId, classId, trainerId, from, to, status, userId, cancellationToken);
        return Ok(sessions);
    }

    [HttpGet("sessions/{sessionId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ClassSessionDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClassSessionDetailDto>> GetSessionDetail(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var detail = await _gymClassService.GetSessionDetailAsync(sessionId, userId, cancellationToken);
        return Ok(detail);
    }

    [HttpGet("trainer/my-sessions")]
    [Authorize(Roles = "TRAINER,ADMIN,Trainer,Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<ClassSessionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassSessionDto>>> GetTrainerSessions(
        [FromQuery] string? filter,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "Vui lòng đăng nhập với tài khoản Huấn luyện viên." });

        var sessions = await _gymClassService.GetTrainerSessionsAsync(userId.Value, filter, cancellationToken);
        return Ok(sessions);
    }

    [HttpPut("sessions/{sessionId:guid}/reschedule")]
    [Authorize(Roles = "ADMIN,Admin")]
    [ProducesResponseType(typeof(ClassSessionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClassSessionDto>> RescheduleSession(
        Guid sessionId,
        [FromBody] RescheduleSessionRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _gymClassService.RescheduleSessionAsync(sessionId, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("sessions/{sessionId:guid}/cancel")]
    [Authorize(Roles = "ADMIN,Admin")]
    [ProducesResponseType(typeof(ClassSessionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClassSessionDto>> CancelSession(
        Guid sessionId,
        [FromBody] CancelSessionRequest? request,
        CancellationToken cancellationToken)
    {
        var updated = await _gymClassService.CancelSessionAsync(
            sessionId, request ?? new CancelSessionRequest(), cancellationToken);
        return Ok(updated);
    }

    [HttpPost("sessions/{sessionId:guid}/attendance")]
    [Authorize(Roles = "TRAINER,ADMIN,Trainer,Admin")]
    [ProducesResponseType(typeof(ConfirmAttendanceResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConfirmAttendanceResultDto>> ConfirmAttendance(
        Guid sessionId,
        [FromBody] ConfirmAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "Vui lòng đăng nhập với tài khoản Huấn luyện viên." });

        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? "TRAINER";
        var result = await _gymClassService.ConfirmAttendanceAsync(
            sessionId, userId.Value, role, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("sessions/process-waitlist-cutoff")]
    [Authorize(Roles = "ADMIN,STAFF,TRAINER,Admin,Staff,Trainer")]
    public async Task<ActionResult> ProcessWaitlistCutoff(CancellationToken cancellationToken)
    {
        var expiredCount = await _classBookingService.ProcessWaitlistCutoffAsync(cancellationToken);
        return Ok(new
        {
            expiredCount,
            message = $"Đã đóng {expiredCount} lượt chờ tại các lớp vượt mốc T-8h."
        });
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("id");
        return Guid.TryParse(userIdStr, out var id) ? id : null;
    }
}
