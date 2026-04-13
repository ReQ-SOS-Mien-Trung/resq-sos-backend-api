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
    IFirebaseService firebaseService,
    ILogger<CloseAssemblyPointCommandHandler> logger)
    : IRequestHandler<CloseAssemblyPointCommand, CloseAssemblyPointResponse>
{
    private readonly IAssemblyPointRepository _assemblyPointRepository = assemblyPointRepository;
    private readonly IRescueTeamRepository _rescueTeamRepository = rescueTeamRepository;
    private readonly IAssemblyEventRepository _assemblyEventRepository = assemblyEventRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDashboardHubService _dashboardHubService = dashboardHubService;
    private readonly IFirebaseService _firebaseService = firebaseService;
    private readonly ILogger<CloseAssemblyPointCommandHandler> _logger = logger;

    public async Task<CloseAssemblyPointResponse> Handle(CloseAssemblyPointCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("CloseAssemblyPoint: Id={Id}", request.Id);

        var assemblyPoint = await _assemblyPointRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("KhÃ´ng tÃ¬m tháº¥y Ä‘iá»ƒm táº­p káº¿t");

        // Kiá»ƒm tra cÃ²n rescuer nÃ o Ä‘Æ°á»£c gÃ¡n vÃ o Ä‘iá»ƒm táº­p káº¿t nÃ y khÃ´ng
        var assignedRescuers = await _assemblyPointRepository.GetAssignedRescuerUserIdsAsync(request.Id, cancellationToken);
        if (assignedRescuers.Count > 0)
        {
            throw new ConflictException(
                $"KhÃ´ng thá»ƒ Ä‘Ã³ng Ä‘iá»ƒm táº­p káº¿t khi váº«n cÃ²n {assignedRescuers.Count} rescuer Ä‘Æ°á»£c gÃ¡n. " +
                "Vui lÃ²ng gá»¡ toÃ n bá»™ rescuer trÆ°á»›c khi Ä‘Ã³ng Ä‘iá»ƒm táº­p káº¿t.");
        }

        // Kiá»ƒm tra cÃ²n Ä‘á»™i cá»©u há»™ nÃ o Ä‘ang hoáº¡t Ä‘á»™ng táº¡i Ä‘iá»ƒm táº­p káº¿t nÃ y khÃ´ng
        var activeTeamCount = await _rescueTeamRepository.CountActiveTeamsByAssemblyPointAsync(
            request.Id,
            Enumerable.Empty<int>(),
            cancellationToken);
        if (activeTeamCount > 0)
        {
            throw new ConflictException(
                $"KhÃ´ng thá»ƒ Ä‘Ã³ng Ä‘iá»ƒm táº­p káº¿t khi váº«n cÃ²n {activeTeamCount} Ä‘á»™i cá»©u há»™ Ä‘ang hoáº¡t Ä‘á»™ng. " +
                "Vui lÃ²ng giáº£i thá»ƒ hoáº·c chuyá»ƒn toÃ n bá»™ Ä‘á»™i sang Ä‘iá»ƒm táº­p káº¿t khÃ¡c trÆ°á»›c khi Ä‘Ã³ng.");
        }

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

        // Domain enforces: chá»‰ Active hoáº·c Overloaded â†’ Closed
        assemblyPoint.ChangeStatus(AssemblyPointStatus.Closed);

        await _assemblyPointRepository.UpdateAsync(assemblyPoint, cancellationToken);
        await _unitOfWork.SaveAsync();

        await _dashboardHubService.PushAssemblyPointSnapshotAsync(
            assemblyPoint.Id,
            "Close",
            cancellationToken);

        _logger.LogInformation("AssemblyPoint closed permanently: Id={Id}", request.Id);

        return new CloseAssemblyPointResponse
        {
            Id = assemblyPoint.Id,
            Status = assemblyPoint.Status.ToString(),
            Message = "Äiá»ƒm táº­p káº¿t Ä‘Ã£ Ä‘Æ°á»£c Ä‘Ã³ng vÄ©nh viá»…n."
        };
    }
}
