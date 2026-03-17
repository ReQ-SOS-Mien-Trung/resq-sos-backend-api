using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Commands.AssignTeamToActivity;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using System.Text.Json;

namespace RESQ.Application.UseCases.Operations.Commands.AddMissionActivity;

public class AddMissionActivityCommandHandler(
    IMissionRepository missionRepository,
    IMissionActivityRepository activityRepository,
    IMissionTeamRepository missionTeamRepository,
    IRescueTeamRepository rescueTeamRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    ILogger<AddMissionActivityCommandHandler> logger
) : IRequestHandler<AddMissionActivityCommand, AddMissionActivityResponse>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly IMissionTeamRepository _missionTeamRepository = missionTeamRepository;
    private readonly IRescueTeamRepository _rescueTeamRepository = rescueTeamRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IMediator _mediator = mediator;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<AddMissionActivityCommandHandler> _logger = logger;

    public async Task<AddMissionActivityResponse> Handle(AddMissionActivityCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Adding activity to MissionId={missionId}", request.MissionId);

        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            throw new NotFoundException($"Không tìm thấy mission với ID: {request.MissionId}");

        // Validate depot inventory if supplies are specified
        if (request.DepotId.HasValue && request.SuppliesToCollect is { Count: > 0 })
        {
            var itemsToCheck = request.SuppliesToCollect
                .Where(s => s.Id.HasValue)
                .Select(s => (
                    ReliefItemId: s.Id!.Value,
                    ItemName: s.Name ?? $"Item#{s.Id}",
                    RequestedQuantity: s.Quantity ?? 0
                ))
                .ToList();

            if (itemsToCheck.Count > 0)
            {
                var shortages = await _depotInventoryRepository.CheckSupplyAvailabilityAsync(
                    request.DepotId.Value, itemsToCheck, cancellationToken);

                if (shortages.Count > 0)
                {
                    var errors = shortages.Select(s => s.NotFound
                        ? $"Vật tư '{s.ItemName}' (ID={s.ReliefItemId}) không có trong kho {request.DepotId}."
                        : $"Vật tư '{s.ItemName}' (ID={s.ReliefItemId}) không đủ số lượng — yêu cầu {s.RequestedQuantity}, khả dụng {s.AvailableQuantity}.");
                    throw new BadRequestException($"Kiểm tra tồn kho thất bại:\n{string.Join("\n", errors)}");
                }
            }
        }

        var activity = new MissionActivityModel
        {
            MissionId = request.MissionId,
            Step = request.Step,
            ActivityCode = request.ActivityCode,
            ActivityType = request.ActivityType,
            Description = request.Description,
            Priority = request.Priority,
            EstimatedTime = request.EstimatedTime,
            SosRequestId = request.SosRequestId,
            DepotId = request.DepotId,
            DepotName = request.DepotName,
            DepotAddress = request.DepotAddress,
            Items = request.SuppliesToCollect is { Count: > 0 }
                ? JsonSerializer.Serialize(request.SuppliesToCollect.Select(s => new SupplyToCollectDto
                {
                    ItemId = s.Id,
                    ItemName = s.Name ?? string.Empty,
                    Quantity = s.Quantity ?? 0,
                    Unit = s.Unit
                }))
                : null,
            Target = request.Target,
            TargetLatitude = request.TargetLatitude,
            TargetLongitude = request.TargetLongitude,
            Status = MissionActivityStatus.Planned
        };

        var activityId = await _activityRepository.AddAsync(activity, cancellationToken);
        await _unitOfWork.SaveAsync();

        var response = new AddMissionActivityResponse
        {
            ActivityId = activityId,
            MissionId = request.MissionId,
            Step = request.Step,
            ActivityType = request.ActivityType,
            Status = "pending"
        };

        if (request.RescueTeamId.HasValue)
        {
            var assignCommand = new AssignTeamToActivityCommand(
                activityId,
                request.MissionId,
                request.RescueTeamId.Value,
                request.AssignedById
            );
            var assignResult = await _mediator.Send(assignCommand, cancellationToken);
            response.MissionTeamId = assignResult.MissionTeamId;
            response.AssignedRescueTeamId = assignResult.RescueTeamId;
        }

        return response;
    }
}
