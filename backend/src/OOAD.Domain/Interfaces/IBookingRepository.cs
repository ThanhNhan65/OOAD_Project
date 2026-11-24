using OOAD.Domain.Entities;
using OOAD.Domain.Enums;

namespace OOAD.Domain.Interfaces;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Booking>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Booking entity, CancellationToken cancellationToken = default);
    void Update(Booking entity);
    void Delete(Booking entity);

    Task<Booking?> FindEligibleBookingForCheckinAsync(
        Guid memberId,
        Guid branchId,
        DateTime checkInUtc,
        CancellationToken cancellationToken = default);
    Task<Booking?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
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

    Task<IReadOnlyList<Booking>> GetActiveByMemberIdsWithDetailsAsync(
        IEnumerable<Guid> memberIds,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Booking>> GetPtBookingsByMemberIdAsync(
        Guid memberId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Booking>> GetPtBookingsByTrainerIdAsync(
        Guid trainerId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);
    Task<Booking?> GetByIdAndMemberIdWithPtDetailsAsync(
        Guid bookingId,
        Guid memberId,
        CancellationToken cancellationToken = default);
    Task<Booking?> GetByIdAndTrainerIdWithPtDetailsAsync(
        Guid bookingId,
        Guid trainerId,
        CancellationToken cancellationToken = default);
    Task<bool> HasPtTimeConflictAsync(
        Guid memberId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Booking>> GetAllPtBookingsWithAvailabilityAsync(
        CancellationToken cancellationToken = default);
}
