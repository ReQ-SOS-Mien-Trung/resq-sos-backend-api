using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.SetAssemblyPointAvailable;

public class SetAssemblyPointAvailableCommandHandler(
    IAssemblyPointRepository repository,
    IUnitOfWork unitOfWork,
    IDashboardHubService dashboardHubService,
    ILogger<SetAssemblyPointAvailableCommandHandler> logger)
    : IRequestHandler<SetAssemblyPointAvailableCommand, SetAssemblyPointAvailableResponse>
{
    private readonly IAssemblyPointRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDashboardHubService _dashboardHubService = dashboardHubService;
    private readonly ILogger<SetAssemblyPointAvailableCommandHandler> _logger = logger;

    public async Task<SetAssemblyPointAvailableResponse> Handle(SetAssemblyPointAvailableCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("SetAssemblyPointAvailable: Id={Id}", request.Id);

        var assemblyPoint = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy điểm tập kết");

        // Domain enforces: Unavailable → Available
        assemblyPoint.ChangeStatus(AssemblyPointStatus.Available, request.ChangedBy, request.Reason);

        await _repository.UpdateAsync(assemblyPoint, cancellationToken);
        await _unitOfWork.SaveAsync();

        await _dashboardHubService.PushAssemblyPointSnapshotAsync(
            assemblyPoint.Id,
            "CompleteMaintenance",
            cancellationToken);

        _logger.LogInformation("AssemblyPoint maintenance completed: Id={Id}", request.Id);

        return new SetAssemblyPointAvailableResponse
        {
            Id = assemblyPoint.Id,
            Status = assemblyPoint.Status.ToString(),
            Message = "Bảo trì hoàn tất. Điểm tập kết đã hoạt động trở lại."
        };
    }
}

