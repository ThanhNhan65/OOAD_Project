using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;
using OOAD.Domain.Entities;
using OOAD.Domain.Exceptions;
using OOAD.Domain.Interfaces;

namespace OOAD.Application.Services.Implementations;

public class TrainerService : ITrainerService
{
    private readonly ITrainerRepository _trainerRepository;
    private readonly ITrainerAvailabilityRepository _availabilityRepository;
    private readonly IClassSessionRepository _classSessionRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TrainerService(
        ITrainerRepository trainerRepository,
        ITrainerAvailabilityRepository availabilityRepository,
        IClassSessionRepository classSessionRepository,
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork)
    {
        _trainerRepository = trainerRepository;
        _availabilityRepository = availabilityRepository;
        _classSessionRepository = classSessionRepository;
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<TrainerDto>> GetAllTrainersAsync(Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        var trainers = await _trainerRepository.GetAllWithDetailsAsync(branchId, cancellationToken);
        return trainers.Select(MapToDto);
    }

    public async Task<TrainerDto?> GetTrainerByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var trainer = await _trainerRepository.GetWithDetailsAsync(id, cancellationToken);
        return trainer == null ? null : MapToDto(trainer);
    }

    public async Task<TrainerDto?> GetCurrentTrainerProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var trainer = await _trainerRepository.GetByUserIdWithDetailsAsync(
            userId,
            includeAvailabilities: true,
            asNoTracking: true,
            cancellationToken);

        return trainer == null ? null : MapToDto(trainer);
    }

    public async Task<TrainerDto> UpdateTrainerProfileAsync(Guid userId, UpdateTrainerProfileRequest request, CancellationToken cancellationToken = default)
    {
        var trainer = await _trainerRepository.GetByUserIdWithDetailsAsync(
            userId,
            includeAvailabilities: false,
            asNoTracking: false,
            cancellationToken);

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

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(trainer);
    }

    public async Task<IEnumerable<TrainerAvailabilityDto>> GetTrainerAvailabilityAsync(Guid trainerId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var list = await _availabilityRepository.GetFilteredByTrainerAsync(trainerId, from, to, cancellationToken);
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
        var trainer = await _trainerRepository.GetByUserIdAsync(userId, cancellationToken);
        if (trainer == null)
            throw new NotFoundException("Trainer profile not found.");

        if (request.StartAt >= request.EndAt)
            throw new ValidationException("Start time must be before End time.");

        if (request.StartAt < DateTime.UtcNow)
            throw new ValidationException("Cannot create availability slots in the past.");

        // Rule: Chặn tạo slot trùng với slot availability khác
        var hasConflictAvailability = await _availabilityRepository.HasConflictAsync(
            trainer.Id,
            request.StartAt,
            request.EndAt,
            excludeId: null,
            cancellationToken);

        if (hasConflictAvailability)
            throw new ValidationException("Khung giờ rảnh này bị trùng với một khung giờ rảnh khác của bạn.");

        // Rule cốt lõi: Chặn tạo slot rảnh trùng giờ dạy lớp nhóm được phân công (ClassSessions)
        var hasConflictClassSession = await _classSessionRepository.HasTrainerSessionConflictAsync(
            trainer.Id,
            request.StartAt,
            request.EndAt,
            excludeSessionId: null,
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

        await _availabilityRepository.AddAsync(availability, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
        var trainer = await _trainerRepository.GetByUserIdAsync(userId, cancellationToken);
        if (trainer == null)
            throw new NotFoundException("Trainer profile not found.");

        var availability = await _availabilityRepository.GetByIdAndTrainerIdAsync(availabilityId, trainer.Id, cancellationToken);
        if (availability == null)
            throw new NotFoundException("Availability slot not found.");

        if (availability.IsBooked)
            throw new ValidationException("Không thể xóa khung giờ đã có học viên đặt lịch.");

        _availabilityRepository.Delete(availability);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<TrainerScheduleItemDto>> GetMyScheduleAsync(Guid userId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var trainer = await _trainerRepository.GetByUserIdAsync(userId, cancellationToken);
        if (trainer == null)
            throw new NotFoundException("Trainer profile not found.");

        var schedule = new List<TrainerScheduleItemDto>();

        // 1. Buổi lớp nhóm (ClassSession)
        var sessions = await _classSessionRepository.GetByTrainerAndRangeAsync(trainer.Id, from, to, cancellationToken);
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
        var ptBookings = await _bookingRepository.GetPtBookingsByTrainerIdAsync(trainer.Id, from, to, cancellationToken);
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
