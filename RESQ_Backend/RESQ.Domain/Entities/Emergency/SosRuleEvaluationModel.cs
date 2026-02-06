namespace RESQ.Domain.Entities.Emergency;

public class SosRuleEvaluationModel
{
    public int Id { get; set; }
    public int SosRequestId { get; set; }
    public double MedicalScore { get; set; }
    public double FoodScore { get; set; }
    public double InjuryScore { get; set; }
    public double MobilityScore { get; set; }
    public double EnvironmentScore { get; set; }
    public double TotalScore { get; set; }
    public string PriorityLevel { get; set; } = string.Empty;
    public string RuleVersion { get; set; } = "1.0";
    public string? ItemsNeeded { get; set; }
    public DateTime CreatedAt { get; set; }

    public SosRuleEvaluationModel() { }
}
