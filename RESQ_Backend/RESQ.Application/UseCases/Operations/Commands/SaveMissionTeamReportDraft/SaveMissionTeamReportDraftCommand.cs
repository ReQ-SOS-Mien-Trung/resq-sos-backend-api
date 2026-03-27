using MediatR;
using RESQ.Application.UseCases.Operations.Queries.GetMissionTeamReport;

namespace RESQ.Application.UseCases.Operations.Commands.SaveMissionTeamReportDraft;

public record SaveMissionTeamReportDraftCommand(
    int MissionId,
    int MissionTeamId,
    Guid SavedBy,
    string? TeamSummary,
    string? TeamNote,
    string? IssuesJson,
    string? ResultJson,
    string? EvidenceJson,
    List<SaveMissionTeamReportDraftActivityItemDto> Activities
) : IRequest<MissionTeamReportResponse>;