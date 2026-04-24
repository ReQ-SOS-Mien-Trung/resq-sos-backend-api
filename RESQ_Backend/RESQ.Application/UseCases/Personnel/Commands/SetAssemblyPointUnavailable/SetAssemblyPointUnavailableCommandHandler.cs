using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.SetAssemblyPointUnavailable;

public class SetAssemblyPointUnavailableCommandHandler(
    IAssemblyPointRepository repository,
    IAssemblyEventRepository assemblyEventRepository,
    IUnitOfWork unitOfWork,
    IDashboardHubService dashboardHubService,
    IOperationalHubService operationalHubService,
    IFirebaseService firebaseService,
    ILogger<SetAssemblyPointUnavailableCommandHandler> logger)
    : IRequestHandler<SetAssemblyPointUnavailableCommand, SetAssemblyPointUnavailableResponse>
{
    private readonly IAssemblyPointRepository _repository = repository;
    private readonly IAssemblyEventRepository _assemblyEventRepository = assemblyEventRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDashboardHubService _dashboardHubService = dashboardHubService;
    private readonly IOperationalHubService _operationalHubService = operationalHubService;
    private readonly IFirebaseService _firebaseService = firebaseService;
    private readonly ILogger<SetAssemblyPointUnavailableCommandHandler> _logger = logger;

    public async Task<SetAssemblyPointUnavailableResponse> Handle(SetAssemblyPointUnavailableCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("SetAssemblyPointUnavailable: Id={Id}", request.Id);

        var assemblyPoint = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy điểm tập kết");

        var activeEvent = await _assemblyEventRepository.GetActiveEventByAssemblyPointAsync(request.Id, cancellationToken);
        if (activeEvent != null)
        {
            await _assemblyEventRepository.UpdateEventStatusAsync(activeEvent.Value.EventId, AssemblyEventStatus.Cancelled.ToString(), cancellationToken);
            var participants = await _assemblyEventRepository.GetParticipantIdsAsync(activeEvent.Value.EventId, cancellationToken);
            foreach (var userId in participants)
            {
                try
                {
                    await _firebaseService.SendNotificationToUserAsync(
                        userId, 
                        "Sự kiện tập hợp đã bị hủy", 
                        $"Điểm tập kết \"{assemblyPoint.Name}\" đang được bảo trì. Sự kiện tập hợp đã bị hủy.", 
                        "assembly_event_cancelled", 
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send notification to user {UserId}", userId);
                }
            }
        }

        // Domain enforces: Available → Unavailable
        assemblyPoint.ChangeStatus(AssemblyPointStatus.Unavailable, request.ChangedBy, request.Reason);

        await _repository.UpdateAsync(assemblyPoint, cancellationToken);
        await _unitOfWork.SaveAsync();

        await Task.WhenAll(
            _dashboardHubService.PushAssemblyPointSnapshotAsync(assemblyPoint.Id, "StartMaintenance", cancellationToken),
            _operationalHubService.PushAssemblyPointListUpdateAsync(cancellationToken));

        // Fetch stationed rescuers to issue an evacuation warning
        var stationedUserIds = await _repository.GetAssignedRescuerUserIdsAsync(assemblyPoint.Id, cancellationToken);
        if (stationedUserIds.Count > 0)
        {
            var title = "🚨 CẢNH BÁO SƠ TÁN KHẨN CẤP 🚨";
            var body = $"Điểm tập kết {assemblyPoint.Name} (Mã: {assemblyPoint.Code}) đã chuyển sang trạng thái KHÔNG KHẢ DỤNG. Tất cả nhân sự đang có mặt tại đây lập tức di tản đến nơi an toàn và chờ lệnh điều phối mới!";
            
            // Fire-and-Forget push notification for all stationed rescuers
            _ = Task.Run(async () =>
            {
                foreach (var userId in stationedUserIds)
                {
                    try
                    {
                        await _firebaseService.SendNotificationToUserAsync(
                            userId,
                            title,
                            body,
                            "EvacuationAlert",
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send evacuation notice to user {UserId}", userId);
                    }
                }
            });
        }

        _logger.LogInformation("AssemblyPoint set to Unavailable: Id={Id}", request.Id);

        return new SetAssemblyPointUnavailableResponse
        {
            Id = assemblyPoint.Id,
            Status = assemblyPoint.Status.ToString(),
            Message = "Điểm tập kết đang trong trạng thái bảo trì."
        };
    }
}

