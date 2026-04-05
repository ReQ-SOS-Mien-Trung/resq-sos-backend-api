using MediatR;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertSosClusterGroupingConfig;

public class UpsertSosClusterGroupingConfigCommandHandler(
    ISosClusterGroupingConfigRepository sosClusterGroupingConfigRepository)
    : IRequestHandler<UpsertSosClusterGroupingConfigCommand, UpsertSosClusterGroupingConfigResponse>
{
    private readonly ISosClusterGroupingConfigRepository _sosClusterGroupingConfigRepository = sosClusterGroupingConfigRepository;

    public async Task<UpsertSosClusterGroupingConfigResponse> Handle(
        UpsertSosClusterGroupingConfigCommand request,
        CancellationToken cancellationToken)
    {
        var saved = await _sosClusterGroupingConfigRepository.UpsertAsync(
            request.MaximumDistanceKm,
            request.UserId,
            cancellationToken);

        return new UpsertSosClusterGroupingConfigResponse
        {
            MaximumDistanceKm = saved.MaximumDistanceKm,
            UpdatedBy = saved.UpdatedBy,
            UpdatedAt = saved.UpdatedAt,
            Message = "Cập nhật khoảng cách gom cluster thành công."
        };
    }
}