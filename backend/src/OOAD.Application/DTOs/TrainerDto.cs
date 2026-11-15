namespace OOAD.Application.DTOs;

public class TrainerDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid BranchId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? BranchName { get; set; }
    public string? Specialty { get; set; }
    public int ExperienceYears { get; set; }
    public string? Bio { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public List<TrainerAvailabilityDto> Availabilities { get; set; } = new();
}

public class TrainerAvailabilityDto
{
    public Guid Id { get; set; }
    public Guid TrainerId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public bool IsBooked { get; set; }
}

public class UpdateTrainerProfileRequest
{
    public string? Specialty { get; set; }
    public int ExperienceYears { get; set; }
    public string? Bio { get; set; }
    public string? Phone { get; set; }
    public Guid? BranchId { get; set; }
}

public class AvailabilityRequest
{
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
}

public class TrainerScheduleItemDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "CLASS"; // "CLASS" | "PT"
    public string Title { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string? Room { get; set; }
    public string? ClientName { get; set; }
    public string? ClientPhone { get; set; }
    public string? Status { get; set; }
}
