using System.Security.Claims;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OOAD.API.Controllers;

[Route("api/reviews")]
public class ReviewsController : ApiControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewsController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>
    /// GET /api/reviews/trainers/{id}
    /// Lấy danh sách đánh giá & thống kê sao của một HLV.
    /// </summary>
    [HttpGet("trainers/{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<TrainerReviewSummaryDto>> GetTrainerReviews(Guid id, CancellationToken cancellationToken)
    {
        var summary = await _reviewService.GetTrainerReviewsAsync(id, cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    /// GET /api/reviews/me
    /// HLV đang đăng nhập lấy danh sách đánh giá của chính mình.
    /// </summary>
    [HttpGet("me")]
    [Authorize(Roles = "TRAINER,ADMIN,Trainer,Admin")]
    public async Task<ActionResult<TrainerReviewSummaryDto>> GetMyReviews(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var summary = await _reviewService.GetMyTrainerReviewsAsync(userId.Value, cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    /// POST /api/reviews
    /// Hội viên gửi đánh giá cho HLV hoặc Lớp học sau buổi tập.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ReviewDto>> SubmitReview([FromBody] CreateReviewRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var review = await _reviewService.SubmitReviewAsync(userId.Value, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, review);
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
