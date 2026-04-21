using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.UpsertAssemblyPointCheckInRadius;

/// <param name="AssemblyPointId">ID của điểm tập kết.</param>
/// <param name="MaxRadiusMeters">Bán kính check-in riêng (mét). Phải > 0.</param>
/// <param name="UpdatedBy">ID người thực hiện cấu hình.</param>
public record UpsertAssemblyPointCheckInRadiusCommand(
    int AssemblyPointId,
    double MaxRadiusMeters,
    Guid UpdatedBy
) : IRequest<UpsertAssemblyPointCheckInRadiusResponse>;
