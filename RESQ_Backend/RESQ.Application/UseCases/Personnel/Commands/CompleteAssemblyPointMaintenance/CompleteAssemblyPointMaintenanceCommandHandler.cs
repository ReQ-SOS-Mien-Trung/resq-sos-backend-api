using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.CompleteAssemblyPointMaintenance;

public class CompleteAssemblyPointMaintenanceCommandHandler(
    IAssemblyPointRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<CompleteAssemblyPointMaintenanceCommandHandler> logger)
    : IRequestHandler<CompleteAssemblyPointMaintenanceCommand, CompleteAssemblyPointMaintenanceResponse>
{
    private readonly IAssemblyPointRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<CompleteAssemblyPointMaintenanceCommandHandler> _logger = logger;

    public async Task<CompleteAssemblyPointMaintenanceResponse> Handle(CompleteAssemblyPointMaintenanceCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("CompleteAssemblyPointMaintenance: Id={Id}", request.Id);

        var assemblyPoint = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy điểm tập kết");

        // Domain enforces: chỉ UnderMaintenance → Active
        assemblyPoint.ChangeStatus(AssemblyPointStatus.Active);

        await _repository.UpdateAsync(assemblyPoint, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("AssemblyPoint maintenance completed: Id={Id}", request.Id);

        return new CompleteAssemblyPointMaintenanceResponse
        {
            Id = assemblyPoint.Id,
            Status = assemblyPoint.Status.ToString(),
            Message = "Bảo trì hoàn tất. Điểm tập kết đã hoạt động trở lại."
        };
    }
}
