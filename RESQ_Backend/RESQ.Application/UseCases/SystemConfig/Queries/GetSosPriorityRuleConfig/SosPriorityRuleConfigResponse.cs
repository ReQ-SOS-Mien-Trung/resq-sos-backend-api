namespace RESQ.Application.UseCases.SystemConfig.Queries.GetSosPriorityRuleConfig;

public class SosPriorityRuleConfigResponse
{
    public int Id { get; set; }
    public string IssueWeightsJson { get; set; } = "{}";
    public string MedicalSevereIssuesJson { get; set; } = "[]";
    public string AgeWeightsJson { get; set; } = "{}";
    public string RequestTypeScoresJson { get; set; } = "{}";
    public string SituationMultipliersJson { get; set; } = "[]";
    public string PriorityThresholdsJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}
