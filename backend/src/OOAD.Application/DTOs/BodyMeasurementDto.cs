namespace OOAD.Application.DTOs;

public class BodyMeasurementDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public DateTime MeasuredAt { get; set; }
    public bool IsInitial { get; set; }
    public string? Note { get; set; }
    public decimal Weight { get; set; }
    public decimal Height { get; set; }
    public decimal Bmi { get; set; }
    public decimal BodyFat { get; set; }
    public decimal MuscleMass { get; set; }
    public decimal Waist { get; set; }
    public decimal Chest { get; set; }
    public decimal Hips { get; set; }
    public Dictionary<string, decimal> Metrics { get; set; } = new();
}

public class CreateBodyMeasurementRequest
{
    public decimal Weight { get; set; }
    public decimal? Height { get; set; }
    public decimal? Bmi { get; set; }
    public decimal? BodyFat { get; set; }
    public decimal? MuscleMass { get; set; }
    public decimal? Waist { get; set; }
    public decimal? Chest { get; set; }
    public decimal? Hips { get; set; }
    public string? Note { get; set; }
    public DateTime? MeasuredAt { get; set; }
}
