using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.SetAssemblyPointUnavailable;

public class SetAssemblyPointUnavailableCommandHandler(
    IAssemblyPointRepository repository,
    IAssemblyEventRepository assemblyEventRepository,
    IMissionActivityRepository missionActivityRepository,
    IRescueTeamRepository rescueTeamRepository,
    IUnitOfWork unitOfWork,
    IDashboardHubService dashboardHubService,
    IOperationalHubService operationalHubService,
    IFirebaseService firebaseService,
    ILogger<SetAssemblyPointUnavailableCommandHandler> logger)
    : IRequestHandler<SetAssemblyPointUnavailableCommand, SetAssemblyPointUnavailableResponse>
{
    private readonly IAssemblyPointRepository _repository = repository;
    private readonly IAssemblyEventRepository _assemblyEventRepository = assemblyEventRepository;
    private readonly IMissionActivityRepository _missionActivityRepository = missionActivityRepository;
    private readonly IRescueTeamRepository _rescueTeamRepository = rescueTeamRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDashboardHubService _dashboardHubService = dashboardHubService;
    private readonly IOperationalHubService _operationalHubService = operationalHubService;
    private readonly IFirebaseService _firebaseService = firebaseService;
    private readonly ILogger<SetAssemblyPointUnavailableCommandHandler> _logger = logger;

    public async Task<SetAssemblyPointUnavailableResponse> Handle(SetAssemblyPointUnavailableCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("SetAssemblyPointUnavailable: Id={Id}", request.Id);

        var eventCancelledUserIds = new List<Guid>();
        SetAssemblyPointUnavailableResponse response = new();

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var assemblyPoint = await _repository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy điểm tập kết.");

            if (assemblyPoint.Status != AssemblyPointStatus.Available)
            {
                throw new ConflictException($"Điểm tập kết phải ở trạng thái Available để bắt đầu luồng đóng. Trạng thái hiện tại: {assemblyPoint.Status}.");
            }

            var checkedInRescuers = await _repository.GetCheckedInRescuersAsync(request.Id, cancellationToken);
            var missionImpacts = await _missionActivityRepository.GetReassignableAssemblyPointImpactsAsync(request.Id, cancellationToken);
            var stationedTeams = await _rescueTeamRepository.GetAvailableStationedTeamsByAssemblyPointAsync(request.Id, cancellationToken);
            var impactedActivityCount = missionImpacts.Sum(x => x.Activities.Count);
            var hasImpact = checkedInRescuers.Count > 0
                || impactedActivityCount > 0
                || stationedTeams.Count > 0;

            if (hasImpact)
            {
                assemblyPoint.ChangeStatus(AssemblyPointStatus.PendingUnavailable, request.ChangedBy, request.Reason);
                await _repository.UpdateAsync(assemblyPoint, cancellationToken);
                await _unitOfWork.SaveAsync();

                response = new SetAssemblyPointUnavailableResponse
                {
                    Id = assemblyPoint.Id,
                    Status = assemblyPoint.Status.ToString(),
                    Message = "Điểm tập kết có tài nguyên bị ảnh hưởng và đang chờ điều phối lại (PendingUnavailable)."
                };
                return;
            }

            var activeEvent = await _assemblyEventRepository.GetActiveEventByAssemblyPointAsync(request.Id, cancellationToken);
            if (activeEvent != null)
            {
                eventCancelledUserIds = await _assemblyEventRepository.GetParticipantIdsAsync(activeEvent.Value.EventId, cancellationToken);
                await _assemblyEventRepository.UpdateEventStatusAsync(activeEvent.Value.EventId, AssemblyEventStatus.Cancelled.ToString(), cancellationToken);
            }

            assemblyPoint.ChangeStatus(AssemblyPointStatus.Unavailable, request.ChangedBy, request.Reason);
            await _repository.UpdateAsync(assemblyPoint, cancellationToken);
            await _unitOfWork.SaveAsync();

            response = new SetAssemblyPointUnavailableResponse
            {
                Id = assemblyPoint.Id,
                Status = assemblyPoint.Status.ToString(),
                Message = "Điểm tập kết đã chuyển sang trạng thái Không khả dụng (Unavailable)."
            };
        });

        await Task.WhenAll(
            _dashboardHubService.PushAssemblyPointSnapshotAsync(request.Id, "StartMaintenance", cancellationToken),
            _operationalHubService.PushAssemblyPointListUpdateAsync(cancellationToken));

        foreach (var userId in eventCancelledUserIds)
        {
            try
            {
                await _firebaseService.SendNotificationToUserAsync(
                    userId,
                    "Assembly event cancelled",
                    "The assembly point is no longer available. The gathering event has been cancelled.",
                    "assembly_event_cancelled",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send notification to user {UserId}", userId);
            }
        }

        if (string.Equals(response.Status, AssemblyPointStatus.PendingUnavailable.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            response.Impact = await BuildImpactResponseAsync(request.Id, cancellationToken);
        }

        return response;
    }

    private async Task<RESQ.Application.Common.Models.AssemblyPointUnavailableImpactResponse> BuildImpactResponseAsync(
        int assemblyPointId,
        CancellationToken cancellationToken)
    {
        var assemblyPoint = await _repository.GetByIdAsync(assemblyPointId, cancellationToken)
            ?? throw new NotFoundException("Assembly point not found.");

        return new RESQ.Application.Common.Models.AssemblyPointUnavailableImpactResponse
        {
            AssemblyPointId = assemblyPoint.Id,
            AssemblyPointCode = assemblyPoint.Code,
            AssemblyPointName = assemblyPoint.Name,
            CurrentStatus = assemblyPoint.Status.ToString(),
            StatusChangedAt = assemblyPoint.StatusChangedAt,
            AvailableAssemblyPoints = await _repository.GetAvailableAlternativesByDistanceAsync(assemblyPointId, cancellationToken),
            RescueTeams = await _missionActivityRepository.GetReassignableAssemblyPointImpactsAsync(assemblyPointId, cancellationToken),
            StationedTeams = await _rescueTeamRepository.GetAvailableStationedTeamsByAssemblyPointAsync(assemblyPointId, cancellationToken),
            TeamlessCheckedInRescuers = await _repository.GetTeamlessCheckedInRescuersAsync(assemblyPointId, cancellationToken),
            CheckedInRescuers = await _repository.GetCheckedInRescuersAsync(assemblyPointId, cancellationToken)
        };
    }
}

