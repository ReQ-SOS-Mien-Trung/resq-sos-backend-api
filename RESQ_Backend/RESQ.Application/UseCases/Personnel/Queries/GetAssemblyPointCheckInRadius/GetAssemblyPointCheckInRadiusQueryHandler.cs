using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.SystemConfig.Queries.GetCheckInRadiusConfig;

namespace RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointCheckInRadius;

public class GetAssemblyPointCheckInRadiusQueryHandler(
    IAssemblyPointRepository assemblyPointRepository,
    IAssemblyPointCheckInRadiusRepository radiusRepository,
    ICheckInRadiusConfigRepository globalRadiusConfigRepository)
    : IRequestHandler<GetAssemblyPointCheckInRadiusQuery, GetAssemblyPointCheckInRadiusResponse>
{
    public async Task<GetAssemblyPointCheckInRadiusResponse> Handle(
        GetAssemblyPointCheckInRadiusQuery request,
        CancellationToken cancellationToken)
    {
        _ = await assemblyPointRepository.GetByIdAsync(request.AssemblyPointId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy điểm tập kết với id = {request.AssemblyPointId}");

        var perPoint = await radiusRepository.GetByAssemblyPointIdAsync(request.AssemblyPointId, cancellationToken);
        if (perPoint != null)
        {
            return new GetAssemblyPointCheckInRadiusResponse
            {
                AssemblyPointId = perPoint.AssemblyPointId,
                MaxRadiusMeters = perPoint.MaxRadiusMeters,
                IsGlobalFallback = false,
                UpdatedAt = perPoint.UpdatedAt
            };
        }

        // Không có cấu hình riêng → trả về cấu hình toàn cục
        var global = await globalRadiusConfigRepository.GetAsync(cancellationToken);
        return new GetAssemblyPointCheckInRadiusResponse
        {
            AssemblyPointId = request.AssemblyPointId,
            MaxRadiusMeters = global?.MaxRadiusMeters ?? GetCheckInRadiusConfigQueryHandler.DefaultMaxRadiusMeters,
            IsGlobalFallback = true,
            UpdatedAt = null
        };
    }
}
