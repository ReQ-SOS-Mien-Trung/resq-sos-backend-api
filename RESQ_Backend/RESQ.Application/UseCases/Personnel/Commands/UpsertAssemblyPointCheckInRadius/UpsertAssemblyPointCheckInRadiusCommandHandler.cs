using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.UpsertAssemblyPointCheckInRadius;

public class UpsertAssemblyPointCheckInRadiusCommandHandler(
    IAssemblyPointRepository assemblyPointRepository,
    IAssemblyPointCheckInRadiusRepository radiusRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpsertAssemblyPointCheckInRadiusCommand, UpsertAssemblyPointCheckInRadiusResponse>
{
    public async Task<UpsertAssemblyPointCheckInRadiusResponse> Handle(
        UpsertAssemblyPointCheckInRadiusCommand request,
        CancellationToken cancellationToken)
    {
        _ = await assemblyPointRepository.GetByIdAsync(request.AssemblyPointId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy điểm tập kết với id = {request.AssemblyPointId}");

        var result = await radiusRepository.UpsertAsync(
            request.AssemblyPointId,
            request.MaxRadiusMeters,
            request.UpdatedBy,
            cancellationToken);

        await unitOfWork.SaveAsync();

        return new UpsertAssemblyPointCheckInRadiusResponse
        {
            AssemblyPointId = result.AssemblyPointId,
            MaxRadiusMeters = result.MaxRadiusMeters,
            UpdatedAt = result.UpdatedAt
        };
    }
}
