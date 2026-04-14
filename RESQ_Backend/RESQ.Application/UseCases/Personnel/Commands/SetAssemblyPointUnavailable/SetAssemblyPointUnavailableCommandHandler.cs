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
    IFirebaseService firebaseService,
    ILogger<SetAssemblyPointUnavailableCommandHandler> logger)
    : IRequestHandler<SetAssemblyPointUnavailableCommand, SetAssemblyPointUnavailableResponse>
{
    private readonly IAssemblyPointRepository _repository = repository;
    private readonly IAssemblyEventRepository _assemblyEventRepository = assemblyEventRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDashboardHubService _dashboardHubService = dashboardHubService;
    private readonly IFirebaseService _firebaseService = firebaseService;
    private readonly ILogger<SetAssemblyPointUnavailableCommandHandler> _logger = logger;

    public async Task<SetAssemblyPointUnavailableResponse> Handle(SetAssemblyPointUnavailableCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("SetAssemblyPointUnavailable: Id={Id}", request.Id);

        var assemblyPoint = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Kh√¥ng t√¨m th·∫•y ƒëi·ªÉm t·∫≠p k·∫øt");

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
                        "S? ki?n t?p h?p d„ thay d?i", 
                        $"–i?m t?p k?t \"{assemblyPoint.Name}\" dang du?c b?o trÏ. S? ki?n t?p h?p d„ b? h?y.", 
                        "assembly_event_completed", 
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send notification to user {UserId}", userId);
                }
            }
        }

        // Domain enforces: ch·ªâ Active ho·∫∑c Overloaded ‚Üí Unavailable    
        assemblyPoint.ChangeStatus(AssemblyPointStatus.Unavailable);

        await _repository.UpdateAsync(assemblyPoint, cancellationToken);
        await _unitOfWork.SaveAsync();

        await _dashboardHubService.PushAssemblyPointSnapshotAsync(
            assemblyPoint.Id,
            "StartMaintenance",
            cancellationToken);

        // Fetch stationed rescuers to issue an evacuation warning
        var stationedUserIds = await _repository.GetAssignedRescuerUserIdsAsync(assemblyPoint.Id, cancellationToken);
        if (stationedUserIds.Count > 0)
        {
            var title = "?? C?NH B¡O SO T¡N KH?N C?P ??";
            var body = $"–i?m t?p k?t {assemblyPoint.Name} (M„: {assemblyPoint.Code}) d„ chuy?n sang tr?ng th·i KH‘NG KH? D?NG. T?t c? nh‚n s? dang cÛ m?t t?i d‚y l?p t?c di t?n d?n noi an to‡n v‡ ch? l?nh di?u ph?i m?i!";
            
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
            Message = "ƒêi·ªÉm t·∫≠p k·∫øt ƒëang trong tr·∫°ng th√°i b·∫£o tr√¨."
        };
    }
}

