namespace OOAD.Application.DTOs;

/// <summary>
/// DTO đại diện cho tham số cấu hình hệ thống (theo Bảng tham số Dạng 2 trong slide 38).
/// </summary>
public class SystemSettingDto
{
    public Guid Id { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
    public string DataType { get; set; } = "STRING";
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedByUserName { get; set; }
}

public class UpdateSystemSettingRequest
{
    public string SettingKey { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
    public string? DataType { get; set; }
    public string? Description { get; set; }
    public Guid? BranchId { get; set; }
}

/// <summary>
/// DTO cấu hình tổng quan hệ thống tương thích trực tiếp với màn hình Admin Settings (slide 37 - Dạng 1).
/// </summary>
public class AppSettingsSummaryDto
{
    public int ReminderHours { get; set; } = 2;
    public int CancelDeadlineHours { get; set; } = 4;
    public int WaitlistCutoffHours { get; set; } = 8;
    public int ExpiryWarningDays { get; set; } = 7;
    public int PtLowSessions { get; set; } = 2;
}

public class UpdateAppSettingsSummaryRequest
{
    public int ReminderHours { get; set; } = 2;
    public int CancelDeadlineHours { get; set; } = 4;
    public int WaitlistCutoffHours { get; set; } = 8;
    public int ExpiryWarningDays { get; set; } = 7;
    public int PtLowSessions { get; set; } = 2;
}
