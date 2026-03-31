namespace RESQ.Domain.Entities.Identity;

public class RescuerScoreModel
{
    public Guid UserId { get; set; }
    public decimal ResponseTimeScore { get; set; }
    public decimal RescueEffectivenessScore { get; set; }
    public decimal DecisionHandlingScore { get; set; }
    public decimal SafetyMedicalSkillScore { get; set; }
    public decimal TeamworkCommunicationScore { get; set; }
    public decimal OverallAverageScore { get; set; }
    public int EvaluationCount { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
