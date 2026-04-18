using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.CloseAssemblyPoint;

public class CloseAssemblyPointCommandHandler(
    IAssemblyPointRepository assemblyPointRepository,
    IRescueTeamRepository rescueTeamRepository,
    IAssemblyEventRepository assemblyEventRepository,
    IUnitOfWork unitOfWork,
    IDashboardHubService dashboardHubService,
    IOperationalHubService operationalHubService,
    IFirebaseService firebaseService,
    ILogger<CloseAssemblyPointCommandHandler> logger)
    : IRequestHandler<CloseAssemblyPointCommand, CloseAssemblyPointResponse>
{
    private readonly IAssemblyPointRepository _assemblyPointRepository = assemblyPointRepository;
    private readonly IRescueTeamRepository _rescueTeamRepository = rescueTeamRepository;
    private readonly IAssemblyEventRepository _assemblyEventRepository = assemblyEventRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDashboardHubService _dashboardHubService = dashboardHubService;
    private readonly IOperationalHubService _operationalHubService = operationalHubService;
    private readonly IFirebaseService _firebaseService = firebaseService;
    private readonly ILogger<CloseAssemblyPointCommandHandler> _logger = logger;

    public async Task<CloseAssemblyPointResponse> Handle(CloseAssemblyPointCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("CloseAssemblyPoint: Id={Id}", request.Id);

        var assemblyPoint = await _assemblyPointRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy điểm tập kết");

        // Kiểm tra còn đội cứu hộ nào đang hoạt động tại điểm tập kết này không
        var activeTeamCount = await _rescueTeamRepository.CountActiveTeamsByAssemblyPointAsync(
            request.Id,
            Enumerable.Empty<int>(),
            cancellationToken);
        if (activeTeamCount > 0)
        {
            throw new ConflictException(
                $"Không thể đóng điểm tập kết khi vẫn còn {activeTeamCount} đội cứu hộ đang hoạt động. " +
                "Vui lòng giải thể hoặc chuyển toàn bộ đội sang điểm tập kết khác trước khi đóng.");
        }

        // Lấy danh sách rescuer trước khi unassign (để gửi thông báo)
        var assignedRescuers = await _assemblyPointRepository.GetAssignedRescuerUserIdsAsync(request.Id, cancellationToken);

        var activeEvent = await _assemblyEventRepository.GetActiveEventByAssemblyPointAsync(request.Id, cancellationToken);
        if (activeEvent != null)
        {
            await _assemblyEventRepository.UpdateEventStatusAsync(activeEvent.Value.EventId, AssemblyEventStatus.Completed.ToString(), cancellationToken);
            var participants = await _assemblyEventRepository.GetParticipantIdsAsync(activeEvent.Value.EventId, cancellationToken);
            foreach (var userId in participants)
            {
                try
                {
                    await _firebaseService.SendNotificationToUserAsync(
                        userId,
                        "Sự kiện tập hợp đã bị hủy",
                        $"Điểm tập kết \"{assemblyPoint.Name}\" đã bị đóng. Sự kiện tập hợp đã kết thúc.",
                        "assembly_event_completed",
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send notification to user {UserId}", userId);
                }
            }
        }

        // Domain enforces: Unavailable → Closed hoặc Created → Closed (Reason bắt buộc)
        assemblyPoint.ChangeStatus(AssemblyPointStatus.Closed, request.ChangedBy, request.Reason);

        await _assemblyPointRepository.UpdateAsync(assemblyPoint, cancellationToken);

        // Auto-unassign toàn bộ rescuer được gán vào AP này
        await _assemblyPointRepository.UnassignAllRescuersAsync(request.Id, cancellationToken);

        await _unitOfWork.SaveAsync();

        await Task.WhenAll(
            _dashboardHubService.PushAssemblyPointSnapshotAsync(assemblyPoint.Id, "Close", cancellationToken),
            _operationalHubService.PushAssemblyPointListUpdateAsync(cancellationToken));

        // Thông báo cho toàn bộ rescuer bị unassign
        if (assignedRescuers.Count > 0)
        {
            _ = Task.Run(async () =>
            {
                foreach (var userId in assignedRescuers)
                {
                    try
                    {
                        await _firebaseService.SendNotificationToUserAsync(
                            userId,
                            "Điểm tập kết đã đóng",
                            $"Điểm tập kết \"{assemblyPoint.Name}\" đã đóng vĩnh viễn. Bạn đã được gỡ khỏi điểm tập kết này. Vui lòng liên hệ quản trị viên để được phân công điểm tập kết mới.",
                            "assembly_point_closed",
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send close notification to user {UserId}", userId);
                    }
                }
            });
        }

        _logger.LogInformation("AssemblyPoint closed permanently: Id={Id}, Reason={Reason}", request.Id, request.Reason);

        return new CloseAssemblyPointResponse
        {
            Id = assemblyPoint.Id,
            Status = assemblyPoint.Status.ToString(),
            Message = "Điểm tập kết đã được đóng vĩnh viễn."
        };
    }
}
