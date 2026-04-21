using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Commands.AssignTeamToActivity;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;
using System.Text.Json;

namespace RESQ.Application.UseCases.Operations.Commands.AddMissionActivity;
using RESQ.Domain.Enum.Logistics;
using RESQ.Application.Repositories.Logistics;

public class AddMissionActivityCommandHandler(
    IMissionRepository missionRepository,
    IMissionActivityRepository activityRepository,
    IMissionTeamRepository missionTeamRepository,
    IRescueTeamRepository rescueTeamRepository,
    ISosRequestRepository sosRequestRepository,
    ISosRequestUpdateRepository sosRequestUpdateRepository,
    IDepotInventoryRepository depotInventoryRepository, IDepotRepository depotRepository,
    IMediator mediator,
    IOperationalHubService operationalHubService,
    IUnitOfWork unitOfWork,
    ILogger<AddMissionActivityCommandHandler> logger
) : IRequestHandler<AddMissionActivityCommand, AddMissionActivityResponse>
{
    private readonly IMissionRepository _missionRepository = missionRepository;
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly IMissionTeamRepository _missionTeamRepository = missionTeamRepository;
    private readonly IRescueTeamRepository _rescueTeamRepository = rescueTeamRepository;
    private readonly ISosRequestRepository _sosRequestRepository = sosRequestRepository;
    private readonly ISosRequestUpdateRepository _sosRequestUpdateRepository = sosRequestUpdateRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IMediator _mediator = mediator;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<AddMissionActivityCommandHandler> _logger = logger;

    private const double DefaultBufferRatio = 0.10;

    public async Task<AddMissionActivityResponse> Handle(AddMissionActivityCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Adding activity to MissionId={missionId}", request.MissionId);

        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            throw new NotFoundException($"Không tìm thấy mission với ID: {request.MissionId}");

                // Validate depot status
        if (request.DepotId.HasValue)
        {
            var status = await _depotRepository.GetStatusByIdAsync(request.DepotId.Value, cancellationToken);
            if (status is null)
                throw new NotFoundException($"Không tìm thấy kho có ID {request.DepotId.Value}.");
                
            if (status is DepotStatus.Unavailable or DepotStatus.Closing or DepotStatus.Closed)
            {
                throw new ConflictException($"Kho {request.DepotId.Value} đang ở trạng thái {status} và không thể sử dụng cho nhiệm vụ.");
            }
        }

        // Validate depot inventory if supplies are specified
        if (request.DepotId.HasValue && request.SuppliesToCollect is { Count: > 0 })
        {
            var itemsToCheck = request.SuppliesToCollect
                .Where(s => s.Id.HasValue)
                .Select(s =>
                {
                    var bufferRatio = Math.Max(0.0, s.BufferRatio ?? DefaultBufferRatio);
                    var bufferQty = bufferRatio > 0 ? (int)Math.Ceiling((s.Quantity ?? 0) * bufferRatio) : 0;
                    return (
                        ItemModelId: s.Id!.Value,
                        ItemName: s.Name ?? $"Item#{s.Id}",
                        RequestedQuantity: (s.Quantity ?? 0) + bufferQty
                    );
                })
                .ToList();

            if (itemsToCheck.Count > 0)
            {
                var shortages = await _depotInventoryRepository.CheckSupplyAvailabilityAsync(
                    request.DepotId.Value, itemsToCheck, cancellationToken);

                if (shortages.Count > 0)
                {
                    var errors = shortages.Select(s => s.NotFound
                        ? $"vật phẩm '{s.ItemName}' (ID={s.ItemModelId}) không có trong kho {request.DepotId}."
                        : $"vật phẩm '{s.ItemName}' (ID={s.ItemModelId}) không đủ số lượng — yêu cầu {s.RequestedQuantity}, khả dụng {s.AvailableQuantity}.");
                    throw new BadRequestException($"Kiểm tra tồn kho thất bại:\n{string.Join("\n", errors)}");
                }
            }
        }

        var victimContext = await LoadVictimContextAsync(request.SosRequestId, cancellationToken);
        var enrichedDescription = MissionActivityVictimContextHelper.ApplySummaryToDescription(
            request.ActivityType,
            request.Description,
            victimContext?.Summary);

        var activity = new MissionActivityModel
        {
            MissionId = request.MissionId,
            Step = request.Step,
            ActivityType = request.ActivityType,
            Description = enrichedDescription,
            Priority = request.Priority,
            EstimatedTime = request.EstimatedTime,
            SosRequestId = request.SosRequestId,
            DepotId = request.DepotId,
            DepotName = request.DepotName,
            DepotAddress = request.DepotAddress,
            Items = request.SuppliesToCollect is { Count: > 0 }
                ? JsonSerializer.Serialize(request.SuppliesToCollect.Select(s =>
                {
                    var bufferRatio = Math.Max(0.0, s.BufferRatio ?? DefaultBufferRatio);
                    var bufferQty = bufferRatio > 0 ? (int)Math.Ceiling((s.Quantity ?? 0) * bufferRatio) : 0;
                    return new SupplyToCollectDto
                    {
                        ItemId = s.Id,
                        ItemName = s.Name ?? string.Empty,
                        Quantity = s.Quantity ?? 0,
                        Unit = s.Unit,
                        BufferRatio = bufferRatio > 0 ? bufferRatio : (double?)null,
                        BufferQuantity = bufferQty > 0 ? bufferQty : (int?)null
                    };
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

        var savedActivity = await _activityRepository.GetByIdAsync(activityId, cancellationToken)
            ?? activity;

        if (request.DepotId.HasValue && request.SuppliesToCollect is { Count: > 0 })
        {
            var itemsToReserve = request.SuppliesToCollect
                .Where(s => s.Id.HasValue && (s.Quantity ?? 0) > 0)
                .Select(s =>
                {
                    var bufferRatio = Math.Max(0.0, s.BufferRatio ?? DefaultBufferRatio);
                    var bufferQty = bufferRatio > 0 ? (int)Math.Ceiling((s.Quantity ?? 0) * bufferRatio) : 0;
                    return (ItemModelId: s.Id!.Value, Quantity: (s.Quantity ?? 0) + bufferQty);
                })
                .ToList();

            if (itemsToReserve.Count > 0)
            {
                try
                {
                    var reservationResult = await _depotInventoryRepository.ReserveSuppliesAsync(
                        request.DepotId.Value,
                        itemsToReserve,
                        cancellationToken);

                    await MissionSupplyExecutionSnapshotHelper.SyncReservationSnapshotAsync(
                        savedActivity,
                        reservationResult,
                        _activityRepository,
                        _logger,
                        cancellationToken);
                    await _unitOfWork.SaveAsync();

                    savedActivity = await _activityRepository.GetByIdAsync(activityId, cancellationToken)
                        ?? savedActivity;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể đặt trước vật phẩm tại kho {DepotId} khi thêm lẻ activity", request.DepotId.Value);
                }
            }
        }

        response.SuppliesToCollect = string.IsNullOrWhiteSpace(savedActivity.Items)
            ? null
            : JsonSerializer.Deserialize<List<SupplyToCollectDto>>(savedActivity.Items, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (savedActivity.DepotId.HasValue
            && IsRealtimeDepotActivity(savedActivity.ActivityType))
        {
            await operationalHubService.PushDepotActivityUpdateAsync(
                new DepotActivityRealtimeUpdate
                {
                    ActivityId = savedActivity.Id,
                    DepotId = savedActivity.DepotId.Value,
                    MissionId = savedActivity.MissionId,
                    MissionTeamId = savedActivity.MissionTeamId,
                    ActivityType = savedActivity.ActivityType,
                    Action = "Created",
                    Status = savedActivity.Status.ToString(),
                    EstimatedTime = savedActivity.EstimatedTime
                },
                cancellationToken);
        }

        return response;
    }

    private static bool IsRealtimeDepotActivity(string? activityType) =>
        string.Equals(activityType, "COLLECT_SUPPLIES", StringComparison.OrdinalIgnoreCase)
        || string.Equals(activityType, "RETURN_SUPPLIES", StringComparison.OrdinalIgnoreCase);

    private async Task<MissionActivityVictimContext?> LoadVictimContextAsync(
        int? sosRequestId,
        CancellationToken cancellationToken)
    {
        if (!sosRequestId.HasValue)
            return null;

        var victimContexts = await MissionActivityVictimContextLoader.LoadAsync(
            [sosRequestId.Value],
            _sosRequestRepository,
            _sosRequestUpdateRepository,
            cancellationToken);

        return victimContexts.GetValueOrDefault(sosRequestId.Value);
    }
}

