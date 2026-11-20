using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;

namespace OOAD.API.Controllers;

/// <summary>
/// Controller đặt/hủy lịch Lớp học nhóm, Hàng chờ Waitlist, Lịch của tôi và Đánh giá buổi học (UC-12, UC-13, UC-32).
/// </summary>
[Route("api/bookings")]
public class BookingsController : ApiControllerBase
{
    private readonly IClassBookingService _classBookingService;

    public BookingsController(IClassBookingService classBookingService)
    {
        _classBookingService = classBookingService;
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(MyScheduleSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MyScheduleSummaryDto>> GetMySchedule(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "Vui lòng đăng nhập để xem lịch của bạn." });

        var summary = await _classBookingService.GetMyScheduleAsync(userId.Value, cancellationToken);
        return Ok(summary);
    }

    [HttpGet("classes")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<BookingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> GetMyClassBookings(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "Vui lòng đăng nhập để xem danh sách đặt lớp." });

        var list = await _classBookingService.GetMyClassBookingsAsync(userId.Value, cancellationToken);
        return Ok(list);
    }

    [HttpPost("classes")]
    [Authorize(Roles = "MEMBER,ADMIN,Member,Admin")]
    [ProducesResponseType(typeof(RegisterClassResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RegisterClassResponseDto>> BookClass(
        [FromBody] BookClassRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "Vui lòng đăng nhập để đặt lớp học nhóm." });

        var result = await _classBookingService.RegisterClassSessionAsync(
            request.ClassSessionId, userId.Value, cancellationToken);
        return Ok(result);
    }

    [HttpPost("classes/{bookingId:guid}/cancel")]
    [Authorize(Roles = "MEMBER,ADMIN,Member,Admin")]
    [ProducesResponseType(typeof(CancelBookingResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CancelBookingResultDto>> CancelClassBooking(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "Vui lòng đăng nhập để hủy lịch." });

        var result = await _classBookingService.CancelClassBookingAsync(bookingId, userId.Value, cancellationToken);
        return Ok(result);
    }

    [HttpPost("waitlist/{waitlistEntryId:guid}/cancel")]
    [Authorize(Roles = "MEMBER,ADMIN,Member,Admin")]
    public async Task<IActionResult> CancelWaitlistEntry(
        Guid waitlistEntryId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "Vui lòng đăng nhập để hủy danh sách chờ." });

        await _classBookingService.CancelWaitlistEntryAsync(waitlistEntryId, userId.Value, cancellationToken);
        return Ok(new { message = "Đã rời khỏi danh sách chờ." });
    }

    [HttpPost("{bookingId:guid}/review")]
    [Authorize(Roles = "MEMBER,ADMIN,Member,Admin")]
    [ProducesResponseType(typeof(BookingReviewDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<BookingReviewDto>> SubmitReview(
        Guid bookingId,
        [FromBody] CreateBookingReviewRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "Vui lòng đăng nhập để gửi đánh giá." });

        var review = await _classBookingService.SubmitBookingReviewAsync(bookingId, userId.Value, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, review);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("id");
        return Guid.TryParse(userIdStr, out var id) ? id : null;
    }
}
