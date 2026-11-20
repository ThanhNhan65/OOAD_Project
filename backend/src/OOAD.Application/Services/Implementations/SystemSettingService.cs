using OOAD.Application.Common.Interfaces;
using OOAD.Application.DTOs;
using OOAD.Application.Services.Interfaces;
using OOAD.Domain.Entities;
using OOAD.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace OOAD.Application.Services.Implementations;

public class SystemSettingService : ISystemSettingService
{
    private readonly IAppDbContext _context;

    public SystemSettingService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.SystemSettings
            .Include(s => s.Branch)
            .Include(s => s.Updater)
            .AsNoTracking();

        if (branchId.HasValue)
        {
            query = query.Where(s => s.BranchId == branchId || s.BranchId == null);
        }

        var list = await query.OrderBy(s => s.SettingKey).ToListAsync(cancellationToken);
        return list.Select(MapToDto).ToList();
    }

    public async Task<SystemSettingDto?> GetByKeyAsync(string key, Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.SystemSettings
            .Include(s => s.Branch)
            .Include(s => s.Updater)
            .AsNoTracking()
            .Where(s => s.SettingKey.ToUpper() == key.ToUpper());

        if (branchId.HasValue)
        {
            query = query.Where(s => s.BranchId == branchId || s.BranchId == null)
                         .OrderByDescending(s => s.BranchId != null);
        }

        var item = await query.FirstOrDefaultAsync(cancellationToken);
        return item == null ? null : MapToDto(item);
    }

    public async Task<AppSettingsSummaryDto> GetSummaryAsync(Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        var settings = await _context.SystemSettings
            .AsNoTracking()
            .Where(s => branchId == null || s.BranchId == branchId || s.BranchId == null)
            .ToListAsync(cancellationToken);

        var summary = new AppSettingsSummaryDto();

        foreach (var s in settings)
        {
            var key = s.SettingKey.ToUpperInvariant();
            if (int.TryParse(s.SettingValue, out var val))
            {
                if (key is "REMINDER_HOURS" or "REMINDERHOURS") summary.ReminderHours = val;
                else if (key is "CANCEL_DEADLINE_HOURS" or "CANCELDEADLINEHOURS" or "CANCEL_BOOKING_MIN_HOURS") summary.CancelDeadlineHours = val;
                else if (key is "WAITLIST_CUTOFF_HOURS" or "WAITLISTCUTOFFHOURS") summary.WaitlistCutoffHours = val;
                else if (key is "EXPIRY_WARNING_DAYS" or "EXPIRYWARNINGDAYS") summary.ExpiryWarningDays = val;
                else if (key is "PT_LOW_SESSIONS" or "PTLOWSESSIONS") summary.PtLowSessions = val;
            }
        }

        return summary;
    }

    public async Task<AppSettingsSummaryDto> UpdateSummaryAsync(UpdateAppSettingsSummaryRequest request, Guid? updaterId = null, Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        if (request.ReminderHours < 1 || request.ReminderHours > 24)
            throw new ValidationException("Thời gian nhắc lịch phải từ 1 đến 24 giờ.");
        if (request.CancelDeadlineHours < 1 || request.CancelDeadlineHours > 48)
            throw new ValidationException("Thời hạn hủy lịch phải từ 1 đến 48 giờ.");
        if (request.WaitlistCutoffHours < 1 || request.WaitlistCutoffHours > 24)
            throw new ValidationException("Mốc hết hạn hàng chờ phải từ 1 đến 24 giờ.");
        if (request.ExpiryWarningDays < 1 || request.ExpiryWarningDays > 30)
            throw new ValidationException("Cảnh báo hết hạn gói phải từ 1 đến 30 ngày.");
        if (request.PtLowSessions < 1 || request.PtLowSessions > 10)
            throw new ValidationException("Cảnh báo số buổi PT phải từ 1 đến 10 buổi.");

        await UpsertInternal("REMINDER_HOURS", request.ReminderHours.ToString(), "INT", "Thời gian nhắc lịch tập sắp diễn ra (giờ)", branchId, updaterId, cancellationToken);
        await UpsertInternal("CANCEL_DEADLINE_HOURS", request.CancelDeadlineHours.ToString(), "INT", "Thời hạn được hủy lịch hoàn trả lượt/buổi (giờ)", branchId, updaterId, cancellationToken);
        await UpsertInternal("WAITLIST_CUTOFF_HOURS", request.WaitlistCutoffHours.ToString(), "INT", "Mốc hết hạn Danh sách chờ (Waitlist Cutoff) (giờ)", branchId, updaterId, cancellationToken);
        await UpsertInternal("EXPIRY_WARNING_DAYS", request.ExpiryWarningDays.ToString(), "INT", "Cảnh báo gói hội viên / PT sắp hết hạn (ngày)", branchId, updaterId, cancellationToken);
        await UpsertInternal("PT_LOW_SESSIONS", request.PtLowSessions.ToString(), "INT", "Cảnh báo gói PT sắp hết số buổi", branchId, updaterId, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new AppSettingsSummaryDto
        {
            ReminderHours = request.ReminderHours,
            CancelDeadlineHours = request.CancelDeadlineHours,
            WaitlistCutoffHours = request.WaitlistCutoffHours,
            ExpiryWarningDays = request.ExpiryWarningDays,
            PtLowSessions = request.PtLowSessions
        };
    }

    public async Task<SystemSettingDto> UpsertSettingAsync(UpdateSystemSettingRequest request, Guid? updaterId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SettingKey))
            throw new ValidationException("Khóa tham số (SettingKey) không được để trống.");

        var setting = await UpsertInternal(
            request.SettingKey.Trim(),
            request.SettingValue.Trim(),
            request.DataType?.Trim() ?? "STRING",
            request.Description?.Trim(),
            request.BranchId,
            updaterId,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        return MapToDto(setting);
    }

    private async Task<SystemSetting> UpsertInternal(
        string key,
        string value,
        string dataType,
        string? description,
        Guid? branchId,
        Guid? updaterId,
        CancellationToken cancellationToken)
    {
        var normalizedKey = key.ToUpperInvariant();
        var existing = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.SettingKey.ToUpper() == normalizedKey && s.BranchId == branchId, cancellationToken);

        if (existing == null)
        {
            existing = new SystemSetting
            {
                SettingKey = normalizedKey,
                SettingValue = value,
                DataType = dataType,
                Description = description,
                BranchId = branchId,
                UpdatedBy = updaterId,
                UpdatedAt = DateTime.UtcNow
            };
            await _context.SystemSettings.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.SettingValue = value;
            existing.DataType = dataType;
            if (!string.IsNullOrEmpty(description)) existing.Description = description;
            existing.UpdatedBy = updaterId;
            existing.UpdatedAt = DateTime.UtcNow;
            _context.SystemSettings.Update(existing);
        }

        return existing;
    }

    private static SystemSettingDto MapToDto(SystemSetting s)
    {
        return new SystemSettingDto
        {
            Id = s.Id,
            BranchId = s.BranchId,
            BranchName = s.Branch?.Name,
            SettingKey = s.SettingKey,
            SettingValue = s.SettingValue,
            DataType = s.DataType,
            Description = s.Description,
            UpdatedAt = s.UpdatedAt,
            UpdatedByUserName = s.Updater?.UserName ?? s.Updater?.FullName
        };
    }
}
