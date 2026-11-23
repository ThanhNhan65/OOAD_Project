using OOAD.Application.DTOs;

namespace OOAD.Application.Services.Interfaces;

public interface IReviewService
{
    Task<TrainerReviewSummaryDto> GetTrainerReviewsAsync(Guid trainerId, CancellationToken cancellationToken = default);
    Task<TrainerReviewSummaryDto> GetMyTrainerReviewsAsync(Guid trainerUserId, CancellationToken cancellationToken = default);
    Task<ReviewDto> SubmitReviewAsync(Guid userId, CreateReviewRequest request, CancellationToken cancellationToken = default);
}
