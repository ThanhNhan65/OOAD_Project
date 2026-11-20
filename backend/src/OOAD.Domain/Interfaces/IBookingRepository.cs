using OOAD.Domain.Entities;
using OOAD.Domain.Enums;

namespace OOAD.Domain.Interfaces;

public interface IBookingRepository : IGenericRepository<Booking>
{
    Task<Booking?> FindEligibleBookingForCheckinAsync(
        Guid memberId,
        Guid branchId,
        DateTime checkInUtc,
        CancellationToken cancellationToken = default
    );

    Task<Booking?> GetByIdWithDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> GetByMemberIdWithDetailsAsync(
        Guid memberId,
        BookingType? type = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> GetByClassSessionIdWithDetailsAsync(
        Guid classSessionId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveClassBookingAsync(
        Guid memberId,
        Guid classSessionId,
        CancellationToken cancellationToken = default);

    Task<bool> HasTimeConflictAsync(
        Guid memberId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        Guid? excludeBookingId = null,
        CancellationToken cancellationToken = default);

    Task<int> CountMonthlyClassBookingsUsedAsync(
        Guid memberId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<int> CountConfirmedSeatsAsync(
        Guid classSessionId,
        CancellationToken cancellationToken = default);
}
