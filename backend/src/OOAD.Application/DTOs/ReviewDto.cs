namespace OOAD.Application.DTOs;

public class ReviewDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public Guid? TrainerId { get; set; }
    public string? TrainerName { get; set; }
    public Guid? BookingId { get; set; }
    public Guid? ClassId { get; set; }
    public string? ClassName { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string TargetType { get; set; } = "trainer"; // "trainer" | "class"
    public DateTime CreatedAt { get; set; }
}

public class CreateReviewRequest
{
    public Guid? TrainerId { get; set; }
    public Guid? BookingId { get; set; }
    public Guid? ClassId { get; set; }
    public int Rating { get; set; } = 5;
    public string? Comment { get; set; }
}

public class TrainerReviewSummaryDto
{
    public Guid TrainerId { get; set; }
    public string TrainerName { get; set; } = string.Empty;
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new();
    public List<ReviewDto> Reviews { get; set; } = new();
}
