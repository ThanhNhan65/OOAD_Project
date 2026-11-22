namespace OOAD.Application.Common.Options;

public sealed class BusinessRuleOptions
{
    public const string SectionName = "BusinessRuleOptions";

    public int WaitlistCutoffHours { get; set; } = 8;
    public int CancelDeadlineHours { get; set; } = 4;
    public int ReminderHoursBeforeSession { get; set; } = 2;
    public int PackageExpiryWarningDays { get; set; } = 7;
    public int PtLowSessionThreshold { get; set; } = 2;
}
