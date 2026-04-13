using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.ActivateAssemblyPoint;

public class ActivateAssemblyPointCommandHandler(
    IAssemblyPointRepository repository,
    IUnitOfWork unitOfWork,
    IDashboardHubService dashboardHubService,
    ILogger<ActivateAssemblyPointCommandHandler> logger)
    : IRequestHandler<ActivateAssemblyPointCommand, ActivateAssemblyPointResponse>
{
    private readonly IAssemblyPointRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDashboardHubService _dashboardHubService = dashboardHubService;
    private readonly ILogger<ActivateAssemblyPointCommandHandler> _logger = logger;

    public async Task<ActivateAssemblyPointResponse> Handle(ActivateAssemblyPointCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("ActivateAssemblyPoint: Id={Id}", request.Id);

        var assemblyPoint = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy điểm tập kết");

        // Domain enforces: chỉ Created → Active
        assemblyPoint.ChangeStatus(AssemblyPointStatus.Active);

        await _repository.UpdateAsync(assemblyPoint, cancellationToken);
        await _unitOfWork.SaveAsync();

        await _dashboardHubService.PushAssemblyPointSnapshotAsync(
            assemblyPoint.Id,
            "Activate",
            cancellationToken);

        _logger.LogInformation("AssemblyPoint activated: Id={Id}", request.Id);

        return new ActivateAssemblyPointResponse
        {
            Id = assemblyPoint.Id,
            Status = assemblyPoint.Status.ToString(),
            Message = "Điểm tập kết đã được kích hoạt thành công."
        };
    }
}
