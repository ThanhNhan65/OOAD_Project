namespace OOAD.Application.DTOs;

public class NutritionPlanDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string? MemberName { get; set; }
    public Guid? CreatedByTrainerId { get; set; }
    public string? TrainerName { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TargetCalories { get; set; }
    public int ProteinGrams { get; set; }
    public int CarbsGrams { get; set; }
    public int FatGrams { get; set; }
    public string? Content { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateNutritionPlanRequest
{
    public Guid MemberId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TargetCalories { get; set; }
    public int ProteinGrams { get; set; }
    public int CarbsGrams { get; set; }
    public int FatGrams { get; set; }
    public string? Content { get; set; }
}
