using OOAD.Application.Common.Interfaces;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace OOAD.Application.Services.Implementations;

public class TrainerService : ITrainerService
{
    private readonly IAppDbContext _context;

    public TrainerService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<TrainerDto>> GetAllTrainersAsync(Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Trainers
            .Include(t => t.User)
            .Include(t => t.Branch)
            .Include(t => t.Reviews)
            .AsNoTracking();

        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            query = query.Where(t => t.BranchId == branchId.Value);
        }

        var trainers = await query.ToListAsync(cancellationToken);

        return trainers.Select(MapToDto);
    }

    public async Task<TrainerDto?> GetTrainerByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var trainer = await _context.Trainers
            .Include(t => t.User)
            .Include(t => t.Branch)
            .Include(t => t.Reviews)
            .Include(t => t.Availabilities.Where(a => a.StartAt >= DateTime.UtcNow && !a.IsBooked))
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return trainer == null ? null : MapToDto(trainer);
    }

    public async Task<TrainerDto?> GetCurrentTrainerProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var trainer = await _context.Trainers
            .Include(t => t.User)
            .Include(t => t.Branch)
            .Include(t => t.Reviews)
            .Include(t => t.Availabilities.OrderBy(a => a.StartAt))
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        return trainer == null ? null : MapToDto(trainer);
    }

    public async Task<TrainerDto> UpdateTrainerProfileAsync(Guid userId, UpdateTrainerProfileRequest request, CancellationToken cancellationToken = default)
    {
        var trainer = await _context.Trainers
            .Include(t => t.User)
            .Include(t => t.Branch)
            .Include(t => t.Reviews)
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        if (trainer == null)
            throw new NotFoundException("Trainer profile not found for current user.");

        if (request.Specialty != null) trainer.Specialty = request.Specialty;
        if (request.ExperienceYears >= 0) trainer.ExperienceYears = request.ExperienceYears;
        if (request.Bio != null) trainer.Bio = request.Bio;
        if (request.BranchId.HasValue && request.BranchId.Value != Guid.Empty)
        {
            trainer.BranchId = request.BranchId.Value;
        }

        if (request.Phone != null && trainer.User != null)
        {
            trainer.User.Phone = request.Phone;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(trainer);
    }

    public async Task<IEnumerable<TrainerAvailabilityDto>> GetTrainerAvailabilityAsync(Guid trainerId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var query = _context.TrainerAvailabilities
            .Where(a => a.TrainerId == trainerId)
            .AsNoTracking();

        if (from.HasValue) query = query.Where(a => a.StartAt >= from.Value);
        if (to.HasValue) query = query.Where(a => a.EndAt <= to.Value);

        var list = await query.OrderBy(a => a.StartAt).ToListAsync(cancellationToken);

        return list.Select(a => new TrainerAvailabilityDto
        {
            Id = a.Id,
            TrainerId = a.TrainerId,
            StartAt = a.StartAt,
            EndAt = a.EndAt,
            IsBooked = a.IsBooked
        });
    }

    public async Task<TrainerAvailabilityDto> AddAvailabilityAsync(Guid userId, AvailabilityRequest request, CancellationToken cancellationToken = default)
    {
        var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);
        if (trainer == null)
            throw new NotFoundException("Trainer profile not found.");

        if (request.StartAt >= request.EndAt)
            throw new ValidationException("Start time must be before End time.");

        if (request.StartAt < DateTime.UtcNow)
            throw new ValidationException("Cannot create availability slots in the past.");

        // Rule: Chặn tạo slot trùng với slot availability khác
        var hasConflictAvailability = await _context.TrainerAvailabilities
            .AnyAsync(a => a.TrainerId == trainer.Id && a.StartAt < request.EndAt && a.EndAt > request.StartAt, cancellationToken);

        if (hasConflictAvailability)
            throw new ValidationException("Khung giờ rảnh này bị trùng với một khung giờ rảnh khác của bạn.");

        // Rule cốt lõi: Chặn tạo slot rảnh trùng giờ dạy lớp nhóm được phân công (ClassSessions)
        var hasConflictClassSession = await _context.ClassSessions
            .AnyAsync(s => s.TrainerId == trainer.Id
                        && s.Status != "CANCELLED"
                        && s.StartAt < request.EndAt
                        && s.EndAt > request.StartAt,
                      cancellationToken);

        if (hasConflictClassSession)
            throw new ValidationException("Khung giờ này trùng với lịch dạy lớp nhóm được phân công của bạn.");

        var availability = new TrainerAvailability
        {
            TrainerId = trainer.Id,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            IsBooked = false
        };

        _context.TrainerAvailabilities.Add(availability);
        await _context.SaveChangesAsync(cancellationToken);

        return new TrainerAvailabilityDto
        {
            Id = availability.Id,
            TrainerId = availability.TrainerId,
            StartAt = availability.StartAt,
            EndAt = availability.EndAt,
            IsBooked = availability.IsBooked
        };
    }

    public async Task DeleteAvailabilityAsync(Guid userId, Guid availabilityId, CancellationToken cancellationToken = default)
    {
        var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);
        if (trainer == null)
            throw new NotFoundException("Trainer profile not found.");

        var availability = await _context.TrainerAvailabilities
            .FirstOrDefaultAsync(a => a.Id == availabilityId && a.TrainerId == trainer.Id, cancellationToken);

        if (availability == null)
            throw new NotFoundException("Availability slot not found.");

        if (availability.IsBooked)
            throw new ValidationException("Không thể xóa khung giờ đã có học viên đặt lịch.");

        _context.TrainerAvailabilities.Remove(availability);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<TrainerScheduleItemDto>> GetMyScheduleAsync(Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);
        if (trainer == null)
            throw new NotFoundException("Trainer profile not found.");

        var schedule = new List<TrainerScheduleItemDto>();

        // 1. Buổi lớp nhóm (ClassSession)
        var sessionQuery = _context.ClassSessions
            .Include(s => s.GymClass)
            .Where(s => s.TrainerId == trainer.Id)
            .AsNoTracking();

        if (from.HasValue) sessionQuery = sessionQuery.Where(s => s.StartAt >= from.Value);
        if (to.HasValue) sessionQuery = sessionQuery.Where(s => s.EndAt <= to.Value);

        var sessions = await sessionQuery.ToListAsync(cancellationToken);
        foreach (var s in sessions)
        {
            schedule.Add(new TrainerScheduleItemDto
            {
                Id = s.Id,
                Type = "CLASS",
                Title = s.GymClass?.Name ?? "Lớp nhóm",
                StartAt = s.StartAt,
                EndAt = s.EndAt,
                Room = s.GymClass?.Room,
                Status = s.Status
            });
        }

        // 2. Lịch PT 1-kèm-1 (Bookings via TrainerAvailability)
        var ptQuery = _context.Bookings
            .Include(b => b.Availability)
            .Include(b => b.Member).ThenInclude(m => m.User)
            .Where(b => b.Availability != null && b.Availability.TrainerId == trainer.Id)
            .AsNoTracking();

        if (from.HasValue) ptQuery = ptQuery.Where(b => b.Availability!.StartAt >= from.Value);
        if (to.HasValue) ptQuery = ptQuery.Where(b => b.Availability!.EndAt <= to.Value);

        var ptBookings = await ptQuery.ToListAsync(cancellationToken);
        foreach (var b in ptBookings)
        {
            schedule.Add(new TrainerScheduleItemDto
            {
                Id = b.Id,
                Type = "PT",
                Title = "Huấn luyện 1-kèm-1",
                StartAt = b.Availability!.StartAt,
                EndAt = b.Availability.EndAt,
                ClientName = b.Member?.User?.FullName ?? "Hội viên",
                ClientPhone = b.Member?.User?.Phone,
                Status = b.Status.ToString()
            });
        }

        return schedule.OrderBy(x => x.StartAt);
    }

    private static TrainerDto MapToDto(Trainer t)
    {
        var reviews = t.Reviews?.ToList() ?? new List<Review>();
        var avg = reviews.Count > 0 ? reviews.Average(r => r.Rating) : 5.0;

        return new TrainerDto
        {
            Id = t.Id,
            UserId = t.UserId,
            BranchId = t.BranchId,
            FullName = t.User?.FullName ?? "HLV",
            Email = t.User?.Email ?? string.Empty,
            Phone = t.User?.Phone,
            BranchName = t.Branch?.Name,
            Specialty = t.Specialty,
            ExperienceYears = t.ExperienceYears,
            Bio = t.Bio,
            AverageRating = Math.Round(avg, 1),
            ReviewCount = reviews.Count,
            Availabilities = t.Availabilities?
                .Select(a => new TrainerAvailabilityDto
                {
                    Id = a.Id,
                    TrainerId = a.TrainerId,
                    StartAt = a.StartAt,
                    EndAt = a.EndAt,
                    IsBooked = a.IsBooked
                })
                .OrderBy(a => a.StartAt)
                .ToList() ?? new List<TrainerAvailabilityDto>()
        };
    }
}
