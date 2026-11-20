using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class BookingRepository : GenericRepository<Booking>, IBookingRepository
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

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

    public async Task<Booking?> GetByIdWithDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .Include(b => b.Member)
                .ThenInclude(m => m.User)
            .Include(b => b.Membership)
                .ThenInclude(ms => ms.Plan)
            .Include(b => b.ClassSession)
                .ThenInclude(cs => cs!.GymClass)
                    .ThenInclude(gc => gc.Branch)
            .Include(b => b.ClassSession)
                .ThenInclude(cs => cs!.Trainer)
                    .ThenInclude(t => t.User)
            .Include(b => b.Availability)
                .ThenInclude(a => a!.Trainer)
                    .ThenInclude(t => t.User)
            .Include(b => b.Availability)
                .ThenInclude(a => a!.Trainer)
                    .ThenInclude(t => t.Branch)
            .Include(b => b.Checkin)
            .Include(b => b.Review)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetByMemberIdWithDetailsAsync(
        Guid memberId,
        BookingType? type = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Bookings
            .AsNoTracking()
            .Include(b => b.ClassSession)
                .ThenInclude(cs => cs!.GymClass)
                    .ThenInclude(gc => gc.Branch)
            .Include(b => b.ClassSession)
                .ThenInclude(cs => cs!.Trainer)
                    .ThenInclude(t => t.User)
            .Include(b => b.Availability)
                .ThenInclude(a => a!.Trainer)
                    .ThenInclude(t => t.User)
            .Include(b => b.Availability)
                .ThenInclude(a => a!.Trainer)
                    .ThenInclude(t => t.Branch)
            .Include(b => b.Checkin)
            .Include(b => b.Review)
            .Where(b => b.MemberId == memberId);

        if (type.HasValue)
            query = query.Where(b => b.Type == type.Value);

        return await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetByClassSessionIdWithDetailsAsync(
        Guid classSessionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .Include(b => b.Member)
                .ThenInclude(m => m.User)
            .Include(b => b.Member)
                .ThenInclude(m => m.MemberCards)
            .Include(b => b.Checkin)
            .Include(b => b.Review)
            .Where(b => b.ClassSessionId == classSessionId)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasActiveClassBookingAsync(
        Guid memberId,
        Guid classSessionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .AnyAsync(b =>
                b.MemberId == memberId &&
                b.ClassSessionId == classSessionId &&
                (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Attended),
                cancellationToken);
    }

    public async Task<bool> HasTimeConflictAsync(
        Guid memberId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        Guid? excludeBookingId = null,
        CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .AnyAsync(b =>
                b.MemberId == memberId &&
                (!excludeBookingId.HasValue || b.Id != excludeBookingId.Value) &&
                (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Pending) &&
                ((b.Type == BookingType.Class &&
                  b.ClassSession != null &&
                  b.ClassSession.Status != "CANCELLED" &&
                  b.ClassSession.StartAt < endAtUtc &&
                  startAtUtc < b.ClassSession.EndAt)
                 ||
                 (b.Type == BookingType.PersonalTrainer &&
                  b.Availability != null &&
                  b.Availability.StartAt < endAtUtc &&
                  startAtUtc < b.Availability.EndAt)),
                cancellationToken);
    }

    public async Task<int> CountMonthlyClassBookingsUsedAsync(
        Guid memberId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var monthStartLocal = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var monthEndLocal = monthStartLocal.AddMonths(1);
        var fromUtc = monthStartLocal.Subtract(VietnamOffset);
        var toUtc = monthEndLocal.Subtract(VietnamOffset);

        return await _context.Bookings
            .CountAsync(b =>
                b.MemberId == memberId &&
                b.Type == BookingType.Class &&
                b.ClassSession != null &&
                b.ClassSession.StartAt >= fromUtc &&
                b.ClassSession.StartAt < toUtc &&
                (b.Status == BookingStatus.Confirmed ||
                 b.Status == BookingStatus.Attended ||
                 b.Status == BookingStatus.NoShow ||
                 b.Status == BookingStatus.LateCancelled),
                cancellationToken);
    }

    public async Task<int> CountConfirmedSeatsAsync(
        Guid classSessionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .CountAsync(b =>
                b.ClassSessionId == classSessionId &&
                (b.Status == BookingStatus.Confirmed ||
                 b.Status == BookingStatus.Attended ||
                 b.Status == BookingStatus.NoShow),
                cancellationToken);
    }
}
