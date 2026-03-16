using MediatR;
using RESQ.Application.UseCases.SystemConfig.Queries.GetSosPriorityRuleConfig;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdateSosPriorityRuleConfig;

public record UpdateSosPriorityRuleConfigCommand(
    int Id,
    string IssueWeightsJson,
    string MedicalSevereIssuesJson,
    string AgeWeightsJson,
    string RequestTypeScoresJson,
    string SituationMultipliersJson,
    string PriorityThresholdsJson
) : IRequest<SosPriorityRuleConfigResponse>;
