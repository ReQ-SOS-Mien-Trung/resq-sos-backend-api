using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.StartAssemblyPointMaintenance;

public class StartAssemblyPointMaintenanceCommandHandler(
    IAssemblyPointRepository repository,
    IUnitOfWork unitOfWork,
    ILogger<StartAssemblyPointMaintenanceCommandHandler> logger)
    : IRequestHandler<StartAssemblyPointMaintenanceCommand, StartAssemblyPointMaintenanceResponse>
{
    private readonly IAssemblyPointRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<StartAssemblyPointMaintenanceCommandHandler> _logger = logger;

    public async Task<StartAssemblyPointMaintenanceResponse> Handle(StartAssemblyPointMaintenanceCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("StartAssemblyPointMaintenance: Id={Id}", request.Id);

        var assemblyPoint = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy điểm tập kết");

        // Domain enforces: chỉ Active hoặc Overloaded → UnderMaintenance
        assemblyPoint.ChangeStatus(AssemblyPointStatus.UnderMaintenance);

        await _repository.UpdateAsync(assemblyPoint, cancellationToken);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation("AssemblyPoint set to UnderMaintenance: Id={Id}", request.Id);

        return new StartAssemblyPointMaintenanceResponse
        {
            Id = assemblyPoint.Id,
            Status = assemblyPoint.Status.ToString(),
            Message = "Điểm tập kết đang trong trạng thái bảo trì."
        };
    }
}
