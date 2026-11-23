using OOAD.Application.DTOs;

namespace OOAD.Application.Services.Interfaces;

public interface IPersonalTrainerService
{
    Task<PtBalanceDto> GetPtBalanceAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<PtBookingDto> BookPtSessionAsync(Guid userId, BookPtRequest request, CancellationToken cancellationToken = default);
    Task<IEnumerable<PtBookingDto>> GetMyPtBookingsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task CancelPtBookingAsync(Guid userId, Guid bookingId, string? reason, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<TrainerPtRequestItemDto>> GetTrainerPtRequestsAsync(Guid trainerUserId, CancellationToken cancellationToken = default);
    Task RespondPtRequestAsync(Guid trainerUserId, Guid bookingId, bool accept, string? reason, CancellationToken cancellationToken = default);
    Task CompletePtSessionAsync(Guid trainerUserId, Guid bookingId, bool attended, CancellationToken cancellationToken = default);
    Task CancelPtByTrainerAsync(Guid trainerUserId, Guid bookingId, string reason, CancellationToken cancellationToken = default);
}
