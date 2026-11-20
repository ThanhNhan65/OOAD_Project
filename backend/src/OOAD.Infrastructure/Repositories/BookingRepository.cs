using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class BookingRepository : GenericRepository<Booking>, IBookingRepository
{
    public BookingRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Booking?> FindEligibleBookingForCheckinAsync(
        Guid memberId,
        Guid branchId,
        DateTime checkInUtc,
        CancellationToken cancellationToken = default
    )
    {
        var earlyWindow = checkInUtc.AddMinutes(60);

        return await _context.Bookings
            .Include(b => b.ClassSession)
                .ThenInclude(cs => cs!.GymClass)
            .Include(b => b.Availability)
                .ThenInclude(a => a!.Trainer)
            .Where(b => b.MemberId == memberId && b.Status == BookingStatus.Confirmed)
            .Where(b =>
                (b.Type == BookingType.Class
                    && b.ClassSession != null
                    && b.ClassSession.GymClass.BranchId == branchId
                    && b.ClassSession.StartAt <= earlyWindow
                    && b.ClassSession.EndAt >= checkInUtc)
                ||
                (b.Type == BookingType.PersonalTrainer
                    && b.Availability != null
                    && b.Availability.Trainer.BranchId == branchId
                    && b.Availability.StartAt <= earlyWindow
                    && b.Availability.EndAt >= checkInUtc))
            .OrderBy(b => b.ClassSession != null ? b.ClassSession.StartAt : b.Availability!.StartAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
