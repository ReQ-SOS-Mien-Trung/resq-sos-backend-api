namespace RESQ.Application.UseCases.Operations.Shared;

public class MissionTeamMemberEvaluationInputDto
{
    public Guid RescuerId { get; set; }
    public decimal ResponseTimeScore { get; set; }
    public decimal RescueEffectivenessScore { get; set; }
    public decimal DecisionHandlingScore { get; set; }
    public decimal SafetyMedicalSkillScore { get; set; }
    public decimal TeamworkCommunicationScore { get; set; }
}
