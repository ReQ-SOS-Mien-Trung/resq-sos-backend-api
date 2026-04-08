using MediatR;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetCheckInRadiusConfig;

public class GetCheckInRadiusConfigQueryHandler(
    ICheckInRadiusConfigRepository checkInRadiusConfigRepository)
    : IRequestHandler<GetCheckInRadiusConfigQuery, GetCheckInRadiusConfigResponse>
{
    /// <summary>Bán kính check-in mặc định khi chưa có cấu hình (200m).</summary>
    public const double DefaultMaxRadiusMeters = 200.0;

    private readonly ICheckInRadiusConfigRepository _checkInRadiusConfigRepository = checkInRadiusConfigRepository;

    public async Task<GetCheckInRadiusConfigResponse> Handle(
        GetCheckInRadiusConfigQuery request,
        CancellationToken cancellationToken)
    {
        var config = await _checkInRadiusConfigRepository.GetAsync(cancellationToken);

        return new GetCheckInRadiusConfigResponse
        {
            MaxRadiusMeters = config?.MaxRadiusMeters ?? DefaultMaxRadiusMeters,
            UpdatedBy = config?.UpdatedBy,
            UpdatedAt = config?.UpdatedAt ?? DateTime.UtcNow
        };
    }
}
