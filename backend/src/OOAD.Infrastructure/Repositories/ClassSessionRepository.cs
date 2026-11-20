using Microsoft.EntityFrameworkCore;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class ClassSessionRepository : GenericRepository<ClassSession>, IClassSessionRepository
{
    public ClassSessionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<ClassSession>> GetSessionsWithDetailsAsync(
        Guid? branchId = null,
        Guid? classId = null,
        Guid? trainerId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ClassSessions
            .AsNoTracking()
            .Include(s => s.GymClass)
                .ThenInclude(c => c.Branch)
            .Include(s => s.GymClass)
                .ThenInclude(c => c.Reviews)
            .Include(s => s.Trainer)
                .ThenInclude(t => t.User)
            .Include(s => s.Bookings.Where(b =>
                b.Status == BookingStatus.Confirmed ||
                b.Status == BookingStatus.Attended ||
                b.Status == BookingStatus.NoShow))
            .Include(s => s.WaitlistEntries.Where(w => w.Status == "WAITING"))
            .AsQueryable();

        if (branchId.HasValue && branchId.Value != Guid.Empty)
            query = query.Where(s => s.GymClass.BranchId == branchId.Value);

        if (classId.HasValue && classId.Value != Guid.Empty)
            query = query.Where(s => s.ClassId == classId.Value);

        if (trainerId.HasValue && trainerId.Value != Guid.Empty)
            query = query.Where(s => s.TrainerId == trainerId.Value || s.Trainer.UserId == trainerId.Value);

        if (fromUtc.HasValue)
            query = query.Where(s => s.EndAt >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(s => s.StartAt <= toUtc.Value);

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var norm = status.Trim().ToUpperInvariant();
            query = query.Where(s => s.Status.ToUpper() == norm);
        }

        return await query
            .OrderBy(s => s.StartAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ClassSession?> GetByIdWithDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.ClassSessions
            .Include(s => s.GymClass)
                .ThenInclude(c => c.Branch)
            .Include(s => s.GymClass)
                .ThenInclude(c => c.Reviews)
            .Include(s => s.Trainer)
                .ThenInclude(t => t.User)
            .Include(s => s.Bookings)
                .ThenInclude(b => b.Member)
                    .ThenInclude(m => m.User)
            .Include(s => s.Bookings)
                .ThenInclude(b => b.Member)
                    .ThenInclude(m => m.MemberCards)
            .Include(s => s.Bookings)
                .ThenInclude(b => b.Checkin)
            .Include(s => s.WaitlistEntries)
                .ThenInclude(w => w.Member)
                    .ThenInclude(m => m.User)
            .Include(s => s.WaitlistEntries)
                .ThenInclude(w => w.Member)
                    .ThenInclude(m => m.Memberships)
                        .ThenInclude(ms => ms.Plan)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<bool> HasRoomConflictAsync(
        Guid branchId,
        string room,
        DateTime startAtUtc,
        DateTime endAtUtc,
        Guid? excludeSessionId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedRoom = room.Trim().ToLower();
        return await _context.ClassSessions
            .AsNoTracking()
            .AnyAsync(s =>
                s.Status != "CANCELLED" &&
                s.GymClass.BranchId == branchId &&
                s.GymClass.Room.Trim().ToLower() == normalizedRoom &&
                (!excludeSessionId.HasValue || s.Id != excludeSessionId.Value) &&
                s.StartAt < endAtUtc &&
                startAtUtc < s.EndAt,
                cancellationToken);
    }

    public async Task<bool> HasTrainerSessionConflictAsync(
        Guid trainerId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        Guid? excludeSessionId = null,
        CancellationToken cancellationToken = default)
    {
        return await _context.ClassSessions
            .AsNoTracking()
            .AnyAsync(s =>
                s.Status != "CANCELLED" &&
                s.TrainerId == trainerId &&
                (!excludeSessionId.HasValue || s.Id != excludeSessionId.Value) &&
                s.StartAt < endAtUtc &&
                startAtUtc < s.EndAt,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ClassSession>> GetSessionsPastWaitlistCutoffWithWaitingEntriesAsync(
        DateTime cutoffThresholdUtc,
        CancellationToken cancellationToken = default)
    {
        return await _context.ClassSessions
            .Include(s => s.GymClass)
            .Include(s => s.WaitlistEntries)
                .ThenInclude(w => w.Member)
            .Where(s =>
                (s.Status == "SCHEDULED" || s.Status == "FULL") &&
                s.StartAt <= cutoffThresholdUtc &&
                s.WaitlistEntries.Any(w => w.Status == "WAITING"))
            .ToListAsync(cancellationToken);
    }
}
