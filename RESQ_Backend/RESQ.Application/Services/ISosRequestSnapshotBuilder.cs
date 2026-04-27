using RESQ.Application.UseCases.Emergency.Queries.GetSosRequests;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.Services;

public interface ISosRequestSnapshotBuilder
{
    Task<SosRequestDetailDto?> BuildAsync(
        int sosRequestId,
        CancellationToken cancellationToken = default);

    Task<SosRequestDetailDto> BuildAsync(
        SosRequestModel sosRequest,
        CancellationToken cancellationToken = default);
}
