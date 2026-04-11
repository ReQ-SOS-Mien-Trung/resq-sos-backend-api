using RESQ.Application.UseCases.Operations.Commands.UpdateMission;
using RESQ.Domain.Entities.Operations;

namespace RESQ.Application.UseCases.Operations.Shared;

public interface IMissionPendingActivityUpdateService
{
    Task ApplyAsync(
        MissionModel mission,
        Guid updatedBy,
        IReadOnlyList<UpdateMissionActivityPatch> activities,
        CancellationToken cancellationToken = default);
}
