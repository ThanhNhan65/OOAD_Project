using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class BookingRepository : IBookingRepository
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private readonly AppDbContext _context;

    public BookingRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Bookings.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Bookings.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Booking entity, CancellationToken cancellationToken = default)
    {
        await _context.Bookings.AddAsync(entity, cancellationToken);
    }

    public void Update(Booking entity)
    {
        _context.Bookings.Update(entity);
    }

    public void Delete(Booking entity)
    {
        _context.Bookings.Remove(entity);
    }

    public async Task<Booking?> FindEligibleBookingForCheckinAsync(
        Guid memberId,
        Guid branchId,
        DateTime checkInUtc,
        CancellationToken cancellationToken = default)
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
            .AsNoTrackingWithIdentityResolution()
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

    public async Task<IReadOnlyList<Booking>> GetActiveByMemberIdsWithDetailsAsync(
        IEnumerable<Guid> memberIds,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default)
    {
        var idList = memberIds.Distinct().ToList();
        if (idList.Count == 0)
            return Array.Empty<Booking>();

        var query = _context.Bookings
            .Include(b => b.ClassSession).ThenInclude(cs => cs!.GymClass)
            .Include(b => b.Availability)
            .Include(b => b.Membership)
            .Where(b => idList.Contains(b.MemberId) &&
                        (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));

        if (asNoTracking)
            query = query.AsNoTrackingWithIdentityResolution();

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetPtBookingsByMemberIdAsync(
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .AsNoTrackingWithIdentityResolution()
            .Include(b => b.Member).ThenInclude(m => m.User)
            .Include(b => b.Availability).ThenInclude(a => a!.Trainer).ThenInclude(t => t.User)
            .Include(b => b.Availability).ThenInclude(a => a!.Trainer).ThenInclude(t => t.Branch)
            .Include(b => b.Membership).ThenInclude(ms => ms.Plan)
            .Include(b => b.Checkin)
            .Include(b => b.Review)
            .Where(b => b.MemberId == memberId && b.Type == BookingType.PersonalTrainer && b.Availability != null)
            .OrderByDescending(b => b.Availability!.StartAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetPtBookingsByTrainerIdAsync(
        Guid trainerId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Bookings
            .AsNoTrackingWithIdentityResolution()
            .Include(b => b.Member).ThenInclude(m => m.User)
            .Include(b => b.Availability)
            .Include(b => b.Membership).ThenInclude(ms => ms.Plan)
            .Include(b => b.Checkin)
            .Where(b => b.Type == BookingType.PersonalTrainer &&
                        b.Availability != null &&
                        b.Availability.TrainerId == trainerId);

        if (from.HasValue)
            query = query.Where(b => b.Availability!.StartAt >= from.Value);

        if (to.HasValue)
            query = query.Where(b => b.Availability!.EndAt <= to.Value);

        return await query
            .OrderByDescending(b => b.Availability!.StartAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Booking?> GetByIdAndMemberIdWithPtDetailsAsync(
        Guid bookingId,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .Include(b => b.Member).ThenInclude(m => m.User)
            .Include(b => b.Availability).ThenInclude(a => a!.Trainer).ThenInclude(t => t.User)
            .Include(b => b.Membership)
            .Include(b => b.ClassSession)
            .Include(b => b.Checkin)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.MemberId == memberId, cancellationToken);
    }

    public async Task<Booking?> GetByIdAndTrainerIdWithPtDetailsAsync(
        Guid bookingId,
        Guid trainerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .Include(b => b.Member).ThenInclude(m => m.User)
            .Include(b => b.Availability).ThenInclude(a => a!.Trainer).ThenInclude(t => t.User)
            .Include(b => b.Membership)
            .Include(b => b.Checkin)
            .FirstOrDefaultAsync(
                b => b.Id == bookingId && b.Availability != null && b.Availability.TrainerId == trainerId,
                cancellationToken);
    }

    public async Task<bool> HasPtTimeConflictAsync(
        Guid memberId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .Include(b => b.Availability)
            .AnyAsync(b => b.MemberId == memberId
                        && b.Status != BookingStatus.Cancelled
                        && b.Status != BookingStatus.Rejected
                        && b.Status != BookingStatus.LateCancelled
                        && b.Status != BookingStatus.AutoCancelled
                        && b.Availability != null
                        && b.Availability.StartAt < endAtUtc
                        && b.Availability.EndAt > startAtUtc,
                      cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetAllPtBookingsWithAvailabilityAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Availability)
            .Where(b => b.Type != BookingType.Class && b.Availability != null)
            .ToListAsync(cancellationToken);
    }
}
