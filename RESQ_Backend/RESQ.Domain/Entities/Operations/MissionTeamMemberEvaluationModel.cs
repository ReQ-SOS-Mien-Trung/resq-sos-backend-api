namespace RESQ.Domain.Entities.Operations;

public class MissionTeamMemberEvaluationModel
{
    public int Id { get; set; }
    public int MissionTeamReportId { get; set; }
    public Guid RescuerId { get; set; }
    public decimal ResponseTimeScore { get; set; }
    public decimal RescueEffectivenessScore { get; set; }
    public decimal DecisionHandlingScore { get; set; }
    public decimal SafetyMedicalSkillScore { get; set; }
    public decimal TeamworkCommunicationScore { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public decimal OverallScore =>
        Math.Round(
            (ResponseTimeScore
             + RescueEffectivenessScore
             + DecisionHandlingScore
             + SafetyMedicalSkillScore
             + TeamworkCommunicationScore) / 5m,
            2,
            MidpointRounding.AwayFromZero);
}
