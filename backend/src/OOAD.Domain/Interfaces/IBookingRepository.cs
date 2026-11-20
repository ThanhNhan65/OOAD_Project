using OOAD.Domain.Entities;

namespace OOAD.Domain.Interfaces;

public interface IBookingRepository : IGenericRepository<Booking>
{
    Task<Booking?> FindEligibleBookingForCheckinAsync(
        Guid memberId,
        Guid branchId,
        DateTime checkInUtc,
        CancellationToken cancellationToken = default
    );
}
