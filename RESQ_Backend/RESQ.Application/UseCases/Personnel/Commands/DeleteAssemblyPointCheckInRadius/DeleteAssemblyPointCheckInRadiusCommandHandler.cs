using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.DeleteAssemblyPointCheckInRadius;

public class DeleteAssemblyPointCheckInRadiusCommandHandler(
    IAssemblyPointRepository assemblyPointRepository,
    IAssemblyPointCheckInRadiusRepository radiusRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteAssemblyPointCheckInRadiusCommand>
{
    public async Task Handle(
        DeleteAssemblyPointCheckInRadiusCommand request,
        CancellationToken cancellationToken)
    {
        _ = await assemblyPointRepository.GetByIdAsync(request.AssemblyPointId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy điểm tập kết với id = {request.AssemblyPointId}");

        var deleted = await radiusRepository.DeleteByAssemblyPointIdAsync(request.AssemblyPointId, cancellationToken);

        if (!deleted)
            throw new NotFoundException(
                $"Điểm tập kết id = {request.AssemblyPointId} chưa có cấu hình bán kính check-in riêng.");

        await unitOfWork.SaveAsync();
    }
}
