using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.Repositories.Operations;

public interface IMissionTeamReportRepository
{
    Task<MissionTeamReportModel?> GetByMissionTeamIdAsync(int missionTeamId, CancellationToken cancellationToken = default);
    Task<int> UpsertDraftAsync(MissionTeamReportModel model, CancellationToken cancellationToken = default);
    Task SubmitAsync(int missionTeamId, Guid submittedBy, CancellationToken cancellationToken = default);
    Task UpdateReportStatusAsync(int missionTeamId, MissionTeamReportStatus status, CancellationToken cancellationToken = default);
}
