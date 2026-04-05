using MediatR;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertRescueTeamRadiusConfig;

public class UpsertRescueTeamRadiusConfigCommandHandler(
    IRescueTeamRadiusConfigRepository rescueTeamRadiusConfigRepository)
    : IRequestHandler<UpsertRescueTeamRadiusConfigCommand, UpsertRescueTeamRadiusConfigResponse>
{
    private readonly IRescueTeamRadiusConfigRepository _rescueTeamRadiusConfigRepository = rescueTeamRadiusConfigRepository;

    public async Task<UpsertRescueTeamRadiusConfigResponse> Handle(
        UpsertRescueTeamRadiusConfigCommand request,
        CancellationToken cancellationToken)
    {
        var saved = await _rescueTeamRadiusConfigRepository.UpsertAsync(
            request.MaxRadiusKm,
            request.UserId,
            cancellationToken);

        return new UpsertRescueTeamRadiusConfigResponse
        {
            MaxRadiusKm = saved.MaxRadiusKm,
            UpdatedBy = saved.UpdatedBy,
            UpdatedAt = saved.UpdatedAt,
            Message = "Cập nhật bán kính tìm kiếm đội cứu hộ thành công."
        };
    }
}
