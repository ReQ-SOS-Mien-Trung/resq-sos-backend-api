using MediatR;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpsertCheckInRadiusConfig;

public class UpsertCheckInRadiusConfigCommandHandler(
    ICheckInRadiusConfigRepository checkInRadiusConfigRepository)
    : IRequestHandler<UpsertCheckInRadiusConfigCommand, UpsertCheckInRadiusConfigResponse>
{
    private readonly ICheckInRadiusConfigRepository _checkInRadiusConfigRepository = checkInRadiusConfigRepository;

    public async Task<UpsertCheckInRadiusConfigResponse> Handle(
        UpsertCheckInRadiusConfigCommand request,
        CancellationToken cancellationToken)
    {
        var saved = await _checkInRadiusConfigRepository.UpsertAsync(
            request.MaxRadiusMeters,
            request.UserId,
            cancellationToken);

        return new UpsertCheckInRadiusConfigResponse
        {
            MaxRadiusMeters = saved.MaxRadiusMeters,
            UpdatedBy = saved.UpdatedBy,
            UpdatedAt = saved.UpdatedAt,
            Message = "Cập nhật bán kính check-in thành công."
        };
    }
}
