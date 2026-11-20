using OOAD.Application.DTOs;

namespace OOAD.Application.Services.Interfaces;

public interface IClassBookingService
{
    Task<RegisterClassResponseDto> RegisterClassSessionAsync(
        Guid classSessionId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<CancelBookingResultDto> CancelClassBookingAsync(
        Guid bookingId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task CancelWaitlistEntryAsync(
        Guid waitlistEntryId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<int> ProcessWaitlistCutoffAsync(
        CancellationToken cancellationToken = default);

    Task<MyScheduleSummaryDto> GetMyScheduleAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BookingDto>> GetMyClassBookingsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<BookingReviewDto> SubmitBookingReviewAsync(
        Guid bookingId,
        Guid currentUserId,
        CreateBookingReviewRequest request,
        CancellationToken cancellationToken = default);
}
