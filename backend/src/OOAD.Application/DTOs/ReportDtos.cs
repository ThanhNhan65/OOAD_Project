namespace OOAD.Application.DTOs.Report;

public record ReportOverviewDto(
    int ActiveMembersNow,
    int TotalMembers,
    List<BranchPresenceDto> BranchPresences,
    List<MonthlyRevenueDto> Revenue,
    List<MonthlyChurnDto> Churn,
    List<PlanUsageDto> PlanUsage,
    List<MonthlyVisitDto> Visits,
    List<HourlyCheckinDto> Hours,
    List<ClassReportDto> Classes,
    List<TrainerReportDto> Trainers,
    List<RecentPaymentDto> RecentPayments
);

public record BranchPresenceDto(
    Guid BranchId,
    string BranchName,
    string OperatingHours,
    int PresentCount
);

public record MonthlyRevenueDto(
    string Key,
    string Label,
    decimal Membership,
    decimal Pt,
    Dictionary<string, decimal> ByBranch
);

public record MonthlyChurnDto(
    string Key,
    string Label,
    int Active,
    int Lost,
    int NewMembers,
    double Rate
);

public record PlanUsageDto(
    string Name,
    int Value,
    string Color
);

public record MonthlyVisitDto(
    string Label,
    int Count
);

public record HourlyCheckinDto(
    string Hour,
    int Count
);

public record ClassReportDto(
    Guid ClassId,
    string ClassName,
    string BranchName,
    string TrainerName,
    int Sessions,
    int FillRate,
    int AttendanceRate,
    int Waitlisted,
    double Rating,
    int ReviewCount
);

public record TrainerReportDto(
    Guid TrainerId,
    string FullName,
    int PtCompleted,
    int PtNoShow,
    int PtRejected,
    int ClassSessions,
    double Rating,
    int ReviewCount
);

public record RecentPaymentDto(
    Guid Id,
    string MemberName,
    string Description,
    decimal Amount,
    DateTime CreatedAt
);