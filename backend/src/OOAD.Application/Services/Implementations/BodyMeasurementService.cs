using Microsoft.EntityFrameworkCore;
using OOAD.Application.Common.Interfaces;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;
using OOAD.Domain.Entities;
using OOAD.Domain.Exceptions;

namespace OOAD.Application.Services.Implementations;

public class BodyMeasurementService : IBodyMeasurementService
{
    private readonly IAppDbContext _context;

    public BodyMeasurementService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<BodyMeasurementDto>> GetMemberMeasurementsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.UserId == userId, cancellationToken);

        if (member == null)
            return Enumerable.Empty<BodyMeasurementDto>();

        var measurements = await _context.BodyMeasurements
            .Include(m => m.Values).ThenInclude(v => v.MetricType)
            .Where(m => m.MemberId == member.Id)
            .OrderBy(m => m.MeasuredAt)
            .ToListAsync(cancellationToken);

        return measurements.Select(MapToDto);
    }

    public async Task<BodyMeasurementDto> CreateMeasurementAsync(Guid userId, CreateBodyMeasurementRequest request, CancellationToken cancellationToken = default)
    {
        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.UserId == userId, cancellationToken);

        if (member == null)
            throw new NotFoundException("Không tìm thấy thông tin hội viên.");

        var metricTypes = await _context.MetricTypes
            .Where(mt => mt.IsActive)
            .ToListAsync(cancellationToken);

        var isFirst = !await _context.BodyMeasurements.AnyAsync(m => m.MemberId == member.Id, cancellationToken);

        var measurement = new BodyMeasurement
        {
            MemberId = member.Id,
            MeasuredAt = request.MeasuredAt ?? DateTime.UtcNow,
            IsInitial = isFirst,
            Note = request.Note
        };

        _context.BodyMeasurements.Add(measurement);

        // Helper to add metric value if metric type exists and request has value
        void AddValue(string code, decimal? val)
        {
            if (!val.HasValue) return;
            var mt = metricTypes.FirstOrDefault(t => t.Code == code);
            if (mt != null)
            {
                measurement.Values.Add(new MeasurementValue
                {
                    MeasurementId = measurement.Id,
                    MetricTypeId = mt.Id,
                    Value = val.Value
                });
            }
        }

        AddValue("WEIGHT", request.Weight);
        AddValue("HEIGHT", request.Height);
        AddValue("BMI", request.Bmi ?? (request.Height.HasValue && request.Height > 0
            ? Math.Round(request.Weight / (request.Height.Value / 100 * (request.Height.Value / 100)), 1)
            : null));
        AddValue("BODY_FAT", request.BodyFat);
        AddValue("MUSCLE_MASS", request.MuscleMass);
        AddValue("WAIST", request.Waist);
        AddValue("CHEST", request.Chest);
        AddValue("HIPS", request.Hips);

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(measurement);
    }

    public async Task<IEnumerable<MetricTypeDto>> GetMetricTypesAsync(CancellationToken cancellationToken = default)
    {
        var list = await _context.MetricTypes
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);

        return list.Select(m => new MetricTypeDto
        {
            Id = m.Id,
            Code = m.Code,
            Name = m.Name,
            Unit = m.Unit,
            IsActive = m.IsActive
        });
    }

    public async Task<IEnumerable<NutritionPlanDto>> GetNutritionPlansAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.UserId == userId, cancellationToken);

        if (member == null)
            return Enumerable.Empty<NutritionPlanDto>();

        var plans = await _context.NutritionPlans
            .Where(p => p.MemberId == member.Id)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        return plans.Select(p => new NutritionPlanDto
        {
            Id = p.Id,
            MemberId = p.MemberId,
            TrainerName = "HLV Dinh Dưỡng OOAD",
            Title = p.Goal,
            TargetCalories = p.DailyCalories,
            ProteinGrams = p.ProteinG,
            CarbsGrams = p.CarbG,
            FatGrams = p.FatG,
            Content = p.MealSuggestion ?? p.DietaryNotes,
            CreatedAt = p.CreatedAt
        });
    }

    public async Task<NutritionPlanDto> CreateNutritionPlanAsync(Guid trainerUserId, CreateNutritionPlanRequest request, CancellationToken cancellationToken = default)
    {
        var trainer = await _context.Trainers
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.UserId == trainerUserId, cancellationToken);

        if (trainer == null)
            throw new NotFoundException("Không tìm thấy thông tin HLV.");

        var member = await _context.Members
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == request.MemberId, cancellationToken);

        if (member == null)
            throw new NotFoundException("Không tìm thấy thông tin hội viên.");

        var plan = new NutritionPlan
        {
            MemberId = member.Id,
            Goal = request.Title,
            DailyCalories = request.TargetCalories,
            ProteinG = request.ProteinGrams,
            CarbG = request.CarbsGrams,
            FatG = request.FatGrams,
            MealSuggestion = request.Content,
            DietaryNotes = "Tư vấn trực tiếp bởi HLV " + (trainer.User?.FullName ?? "chuyên nghiệp"),
            CreatedAt = DateTime.UtcNow
        };

        _context.NutritionPlans.Add(plan);
        await _context.SaveChangesAsync(cancellationToken);

        return new NutritionPlanDto
        {
            Id = plan.Id,
            MemberId = member.Id,
            MemberName = member.User?.FullName,
            CreatedByTrainerId = trainer.Id,
            TrainerName = trainer.User?.FullName ?? "HLV OOAD",
            Title = plan.Goal,
            TargetCalories = plan.DailyCalories,
            ProteinGrams = plan.ProteinG,
            CarbsGrams = plan.CarbG,
            FatGrams = plan.FatG,
            Content = plan.MealSuggestion,
            CreatedAt = plan.CreatedAt
        };
    }

    private static BodyMeasurementDto MapToDto(BodyMeasurement m)
    {
        var dict = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in m.Values)
        {
            if (v.MetricType != null)
            {
                dict[v.MetricType.Code] = v.Value;
            }
        }

        dict.TryGetValue("WEIGHT", out var w);
        dict.TryGetValue("HEIGHT", out var h);
        dict.TryGetValue("BMI", out var bmi);
        dict.TryGetValue("BODY_FAT", out var fat);
        dict.TryGetValue("MUSCLE_MASS", out var muscle);
        dict.TryGetValue("WAIST", out var waist);
        dict.TryGetValue("CHEST", out var chest);
        dict.TryGetValue("HIPS", out var hips);

        return new BodyMeasurementDto
        {
            Id = m.Id,
            MemberId = m.MemberId,
            MeasuredAt = m.MeasuredAt,
            IsInitial = m.IsInitial,
            Note = m.Note,
            Weight = w,
            Height = h,
            Bmi = bmi,
            BodyFat = fat,
            MuscleMass = muscle,
            Waist = waist,
            Chest = chest,
            Hips = hips,
            Metrics = dict
        };
    }
}
