using OOAD.Application.DTOs;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;

namespace OOAD.Application.Services.Implementations;

internal static class SchedulingHelpers
{
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static DateTime Utc(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    public static DateTime? Utc(DateTime? dt) =>
        dt.HasValue ? Utc(dt.Value) : null;

    public static DateTime ToVietnamDate(DateTime utcDt) =>
        Utc(utcDt).Add(VietnamOffset).Date;

    public static TimeSpan ParseTimeSpan(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value) || !TimeSpan.TryParse(value.Trim(), out var parsed))
        {
            throw new Domain.Exceptions.ValidationException($"{fieldName} không đúng định dạng giờ (HH:mm).");
        }
        return parsed;
    }

    public static List<int> NormalizeDaysOfWeek(int? singleDay, IEnumerable<int>? daysOfWeek)
    {
        var raw = new List<int>();
        if (daysOfWeek != null)
            raw.AddRange(daysOfWeek);
        if (raw.Count == 0 && singleDay.HasValue)
            raw.Add(singleDay.Value);

        if (raw.Count == 0)
            throw new Domain.Exceptions.ValidationException("Vui lòng chọn ít nhất một ngày trong tuần.");

        var normalized = new List<int>();
        foreach (var d in raw)
        {
            var mapped = d == 7 ? 0 : d;
            if (mapped < 0 || mapped > 6)
                throw new Domain.Exceptions.ValidationException("Thứ trong tuần không hợp lệ (chấp nhận 0–7).");
            if (!normalized.Contains(mapped))
                normalized.Add(mapped);
        }

        return normalized.OrderBy(x => x).ToList();
    }

    public static (string Category, string Color) InferCategoryAndColor(string className, string? room)
    {
        var text = $"{className} {room}".ToLowerInvariant();
        if (text.Contains("yoga")) return ("Yoga", "#2ed47a");
        if (text.Contains("hiit") || text.Contains("tabata") || text.Contains("đốt mỡ")) return ("HIIT", "#ff5c8a");
        if (text.Contains("boxing") || text.Contains("kick") || text.Contains("muay")) return ("Boxing", "#ffb020");
        if (text.Contains("pilates") || text.Contains("core")) return ("Pilates", "#a259ff");
        if (text.Contains("cycling") || text.Contains("spin") || text.Contains("đạp")) return ("Cycling", "#3fa9ff");
        if (text.Contains("zumba") || text.Contains("dance")) return ("Dance", "#ff7a45");
        return ("Fitness", "#7c5cff");
    }

    public static (bool Ok, Membership? Membership, string? ErrorReason) EvaluateMembershipForClass(
        IReadOnlyList<Membership> memberships,
        Guid branchId,
        string branchName,
        DateTime sessionVnDate)
    {
        var gymMemberships = memberships
            .Where(m => m.Plan != null && m.Plan.PlanType.Equals("MEMBERSHIP", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.Plan.PriorityLevel)
            .ThenByDescending(m => m.EndDate)
            .ToList();

        if (gymMemberships.Count == 0)
        {
            return (false, null, "Bạn chưa có gói hội viên nào. Vui lòng đăng ký gói tập trước khi đặt lớp.");
        }

        // Kiểm tra xem có gói nào đang hiệu lực vào ngày buổi học hay không
        var coveringMemberships = gymMemberships
            .Where(m =>
                (m.Status == MembershipStatus.Active || m.Status == MembershipStatus.Frozen) &&
                m.StartDate.Date <= sessionVnDate &&
                sessionVnDate <= m.EndDate.Date)
            .ToList();

        if (coveringMemberships.Count == 0)
        {
            return (false, null, $"Gói tập của bạn không còn hiệu lực vào ngày {sessionVnDate:dd/MM/yyyy}. Vui lòng gia hạn gói tập.");
        }

        var nonFrozen = coveringMemberships
            .Where(m =>
                m.Status == MembershipStatus.Active &&
                !m.FreezeRequests.Any(fr =>
                    fr.Status == FreezeRequestStatus.Approved &&
                    fr.FromDate.Date <= sessionVnDate &&
                    sessionVnDate <= fr.ToDate.Date))
            .ToList();

        if (nonFrozen.Count == 0)
        {
            return (false, null, $"Gói tập của bạn đang trong thời gian tạm ngưng (Freeze) vào ngày {sessionVnDate:dd/MM/yyyy}.");
        }

        var branchEligible = nonFrozen.FirstOrDefault(m =>
            m.Plan.IsAllBranches ||
            m.BranchId == branchId ||
            m.Plan.PlanBranches.Any(pb => pb.BranchId == branchId));

        if (branchEligible == null)
        {
            return (false, null, $"Gói tập '{nonFrozen[0].Plan.Name}' không hỗ trợ đặt lớp tại chi nhánh {branchName}.");
        }

        return (true, branchEligible, null);
    }

    public static Notification BuildNotification(
        Guid userId,
        string type,
        string title,
        string content,
        string? refType = null,
        Guid? refId = null) => new()
    {
        UserId = userId,
        Type = type,
        Title = title,
        Content = content,
        IsRead = false,
        CreatedAt = DateTime.UtcNow,
        RefType = refType,
        RefId = refId
    };

    public static WaitlistEntryDto MapWaitlistDto(WaitlistEntry w, int position)
    {
        var session = w.ClassSession;
        var gymClass = session?.GymClass;
        var (category, color) = InferCategoryAndColor(gymClass?.Name ?? string.Empty, gymClass?.Room);
        var activeMembership = w.Member?.Memberships?
            .Where(m => m.Status == MembershipStatus.Active)
            .OrderByDescending(m => m.Plan?.PriorityLevel ?? 0)
            .ThenByDescending(m => m.EndDate)
            .FirstOrDefault();

        return new WaitlistEntryDto
        {
            Id = w.Id,
            ClassSessionId = w.ClassSessionId,
            ClassName = gymClass?.Name ?? string.Empty,
            Category = category,
            Color = color,
            BranchId = gymClass?.BranchId ?? Guid.Empty,
            BranchName = gymClass?.Branch?.Name ?? string.Empty,
            Room = gymClass?.Room ?? string.Empty,
            TrainerName = session?.Trainer?.User?.FullName ?? string.Empty,
            StartAt = session != null ? Utc(session.StartAt) : DateTime.UtcNow,
            EndAt = session != null ? Utc(session.EndAt) : DateTime.UtcNow,
            MemberId = w.MemberId,
            UserId = w.Member?.UserId ?? Guid.Empty,
            MemberName = w.Member?.User?.FullName ?? string.Empty,
            MemberEmail = w.Member?.User?.Email ?? string.Empty,
            PlanName = activeMembership?.Plan?.Name,
            PriorityLevel = w.PriorityLevel,
            Position = position,
            RegisteredAt = Utc(w.RegisteredAt),
            Status = w.Status
        };
    }

    public static BookingDto MapBookingDto(Booking b, int cancelDeadlineHours)
    {
        var nowUtc = DateTime.UtcNow;

        if (b.Type == BookingType.Class && b.ClassSession != null)
        {
            var s = b.ClassSession;
            var gc = s.GymClass;
            var (category, color) = InferCategoryAndColor(gc?.Name ?? string.Empty, gc?.Room);
            var startAt = Utc(s.StartAt);
            var endAt = Utc(s.EndAt);
            var hoursUntil = (startAt - nowUtc).TotalHours;
            var canCancel = b.Status == BookingStatus.Confirmed && startAt > nowUtc && s.Status != "CANCELLED";

            return new BookingDto
            {
                Id = b.Id,
                MemberId = b.MemberId,
                MembershipId = b.MembershipId,
                Type = "Class",
                ClassSessionId = b.ClassSessionId,
                AvailabilityId = null,
                ClassId = gc?.Id,
                Status = b.Status.ToString(),
                CreatedAt = Utc(b.CreatedAt),
                CancelledAt = Utc(b.CancelledAt),
                Title = gc?.Name ?? "Lớp học nhóm",
                Category = category,
                Color = color,
                BranchId = gc?.BranchId ?? Guid.Empty,
                BranchName = gc?.Branch?.Name ?? string.Empty,
                Room = gc?.Room ?? string.Empty,
                TrainerId = s.TrainerId,
                TrainerName = s.Trainer?.User?.FullName ?? string.Empty,
                StartAt = startAt,
                EndAt = endAt,
                CanCancel = canCancel,
                IsCancelTimely = hoursUntil >= cancelDeadlineHours,
                CancelDeadlineHours = cancelDeadlineHours,
                CanReview = b.Status == BookingStatus.Attended && b.Review == null,
                IsReviewed = b.Review != null,
                CheckedIn = b.Checkin != null || b.Status == BookingStatus.Attended
            };
        }

        // PT Booking
        var avail = b.Availability;
        var ptStart = avail != null ? Utc(avail.StartAt) : Utc(b.CreatedAt);
        var ptEnd = avail != null ? Utc(avail.EndAt) : Utc(b.CreatedAt);
        var ptHoursUntil = (ptStart - nowUtc).TotalHours;
        var ptCanCancel = (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed) && ptStart > nowUtc;

        return new BookingDto
        {
            Id = b.Id,
            MemberId = b.MemberId,
            MembershipId = b.MembershipId,
            Type = "PersonalTrainer",
            ClassSessionId = null,
            AvailabilityId = b.AvailabilityId,
            ClassId = null,
            Status = b.Status.ToString(),
            CreatedAt = Utc(b.CreatedAt),
            CancelledAt = Utc(b.CancelledAt),
            Title = $"PT 1-kèm-1 · {avail?.Trainer?.User?.FullName ?? "Huấn luyện viên"}",
            Category = "PT 1-kèm-1",
            Color = "#00c2a8",
            BranchId = avail?.Trainer?.BranchId ?? Guid.Empty,
            BranchName = avail?.Trainer?.Branch?.Name ?? string.Empty,
            Room = "Khu PT",
            TrainerId = avail?.TrainerId ?? Guid.Empty,
            TrainerName = avail?.Trainer?.User?.FullName ?? string.Empty,
            StartAt = ptStart,
            EndAt = ptEnd,
            CanCancel = ptCanCancel,
            IsCancelTimely = b.Status == BookingStatus.Pending || ptHoursUntil >= cancelDeadlineHours,
            CancelDeadlineHours = cancelDeadlineHours,
            CanReview = b.Status == BookingStatus.Attended && b.Review == null,
            IsReviewed = b.Review != null,
            CheckedIn = b.Checkin != null || b.Status == BookingStatus.Attended
        };
    }
}
