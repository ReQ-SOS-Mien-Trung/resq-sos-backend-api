namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdateSosPriorityRuleConfig;

public class UpdateSosPriorityRuleConfigRequestDto
{
    public string IssueWeightsJson { get; set; } = "{}";
    public string MedicalSevereIssuesJson { get; set; } = "[]";
    public string AgeWeightsJson { get; set; } = "{}";
    public string RequestTypeScoresJson { get; set; } = "{}";
    public string SituationMultipliersJson { get; set; } = "[]";
    public string PriorityThresholdsJson { get; set; } = "{}";
}
