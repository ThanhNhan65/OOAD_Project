namespace OOAD.Application.DTOs;

public class GymClassDto
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public Guid TrainerId { get; set; }
    public Guid TrainerUserId { get; set; }
    public string TrainerName { get; set; } = string.Empty;
    public string? TrainerSpecialty { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int DayOfWeek { get; set; }
    public List<int> DaysOfWeek { get; set; } = new();
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Color { get; set; } = "#7c5cff";
    public int UpcomingSessionCount { get; set; }
    public bool Active { get; set; } = true;
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
}

public class CreateGymClassRequest
{
    public Guid BranchId { get; set; }
    public Guid TrainerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public int Capacity { get; set; } = 20;
    public int? DayOfWeek { get; set; }
    public List<int>? DaysOfWeek { get; set; }
    public string StartTime { get; set; } = "07:00";
    public string? EndTime { get; set; }
    public int? DurationMinutes { get; set; }
    public int GenerateWeeks { get; set; } = 3;
}

public class UpdateGymClassRequest
{
    public Guid BranchId { get; set; }
    public Guid TrainerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public int Capacity { get; set; } = 20;
    public int? DayOfWeek { get; set; }
    public List<int>? DaysOfWeek { get; set; }
    public string StartTime { get; set; } = "07:00";
    public string? EndTime { get; set; }
    public int? DurationMinutes { get; set; }
    public bool RegenerateFutureSessions { get; set; } = true;
    public int GenerateWeeks { get; set; } = 3;
}

public class GenerateSessionsRequest
{
    public int Weeks { get; set; } = 2;
    public List<int>? DaysOfWeek { get; set; }
}
