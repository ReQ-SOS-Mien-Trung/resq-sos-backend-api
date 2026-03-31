using MediatR;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Application.UseCases.Operations.Queries.GetMissionTeamReport;

namespace RESQ.Application.UseCases.Operations.Commands.SubmitMissionTeamReport;

public record SubmitMissionTeamReportCommand(
    int MissionId,
    int MissionTeamId,
    Guid SubmittedBy,
    string? TeamSummary,
    string? TeamNote,
    string? IssuesJson,
    string? ResultJson,
    string? EvidenceJson,
    List<SubmitMissionTeamReportActivityItemDto> Activities,
    List<MissionTeamMemberEvaluationInputDto> MemberEvaluations
) : IRequest<MissionTeamReportResponse>;
