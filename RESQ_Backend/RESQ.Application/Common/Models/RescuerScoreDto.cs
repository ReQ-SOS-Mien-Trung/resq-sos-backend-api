using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.Common.Models;

public class RescuerScoreDto
{
    public decimal ResponseTimeScore { get; set; }
    public decimal RescueEffectivenessScore { get; set; }
    public decimal DecisionHandlingScore { get; set; }
    public decimal SafetyMedicalSkillScore { get; set; }
    public decimal TeamworkCommunicationScore { get; set; }
    public decimal OverallAverageScore { get; set; }
    public int EvaluationCount { get; set; }

    public static RescuerScoreDto? FromModel(RescuerScoreModel? model)
    {
        if (model is null)
        {
            return null;
        }

        return new RescuerScoreDto
        {
            ResponseTimeScore = model.ResponseTimeScore,
            RescueEffectivenessScore = model.RescueEffectivenessScore,
            DecisionHandlingScore = model.DecisionHandlingScore,
            SafetyMedicalSkillScore = model.SafetyMedicalSkillScore,
            TeamworkCommunicationScore = model.TeamworkCommunicationScore,
            OverallAverageScore = model.OverallAverageScore,
            EvaluationCount = model.EvaluationCount
        };
    }
}
