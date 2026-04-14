using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.Operations;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Shared;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMissionActivity;

public class UpdateMissionActivityCommandHandler(
    IMissionActivityRepository activityRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IAssemblyPointRepository assemblyPointRepository,
    IUnitOfWork unitOfWork,
    ILogger<UpdateMissionActivityCommandHandler> logger
) : IRequestHandler<UpdateMissionActivityCommand, UpdateMissionActivityResponse>
{
    private readonly IMissionActivityRepository _activityRepository = activityRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IAssemblyPointRepository _assemblyPointRepository = assemblyPointRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<UpdateMissionActivityCommandHandler> _logger = logger;

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };
    private const string ReturnAssemblyPointActivityType = "RETURN_ASSEMBLY_POINT";

    public async Task<UpdateMissionActivityResponse> Handle(UpdateMissionActivityCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating ActivityId={activityId}", request.ActivityId);

        var activity = await _activityRepository.GetByIdAsync(request.ActivityId, cancellationToken);
        if (activity is null)
            throw new NotFoundException($"Không tìm thấy activity với ID: {request.ActivityId}");

        if (activity.Status != MissionActivityStatus.Planned
            && !CanUpdateOngoingReturnAssemblyPoint(activity, request))
        {
            throw new BadRequestException(
                $"Chi duoc cap nhat activity Planned. Rieng RETURN_ASSEMBLY_POINT dang OnGoing chi duoc doi AssemblyPointId va/hoac Description. Activity #{activity.Id} hien o trang thai {activity.Status}.");
        }

        if (request.AssemblyPointId.HasValue
            && (request.TargetLatitude.HasValue || request.TargetLongitude.HasValue))
        {
            throw new BadRequestException("Khong duoc gui TargetLatitude/TargetLongitude khi cap nhat AssemblyPointId.");
        }

        // 1. Release old reservations if they exist
        if (request.Items is not null
            && activity.DepotId.HasValue
            && !string.IsNullOrWhiteSpace(activity.Items))
        {
            try
            {
                var oldItems = JsonSerializer.Deserialize<List<SupplyToCollectDto>>(activity.Items, _jsonOpts);
                if (oldItems is { Count: > 0 })
                {
                    var itemsToRelease = oldItems
                        .Where(i => i.ItemId.HasValue && i.Quantity > 0)
                        .Select(i => (ItemModelId: i.ItemId!.Value, Quantity: i.Quantity + (i.BufferQuantity ?? 0)))
                        .ToList();

                    if (itemsToRelease.Count > 0)
                        await _depotInventoryRepository.ReleaseReservedSuppliesAsync(activity.DepotId.Value, itemsToRelease, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi giải phóng vật phẩm cũ cho activity #{ActivityId}", activity.Id);
            }
        }

        // 2. Validate NEW supplies if specified
        int? newDepotId = activity.DepotId; // Currently we don't allow updating DepotId via this command but let's be safe
        var newItemsRaw = request.Items;
        List<SupplyToCollectDto>? nextItems = null;

        if (!string.IsNullOrWhiteSpace(newItemsRaw))
        {
            try
            {
                nextItems = JsonSerializer.Deserialize<List<SupplyToCollectDto>>(newItemsRaw, _jsonOpts);
                if (nextItems is { Count: > 0 } && newDepotId.HasValue)
                {
                    var itemsToCheck = nextItems
                        .Where(i => i.ItemId.HasValue && i.Quantity > 0)
                        .Select(i => (ItemModelId: i.ItemId!.Value, ItemName: i.ItemName ?? $"Item#{i.ItemId}", RequestedQuantity: i.Quantity))
                        .ToList();

                    if (itemsToCheck.Count > 0)
                    {
                        var shortages = await _depotInventoryRepository.CheckSupplyAvailabilityAsync(newDepotId.Value, itemsToCheck, cancellationToken);
                        if (shortages.Count > 0)
                        {
                            var errors = shortages.Select(s => s.NotFound
                                ? $"Kho {newDepotId}: vật phẩm '{s.ItemName}' không có trong kho."
                                : $"Kho {newDepotId}: vật phẩm '{s.ItemName}' không đủ — yêu cầu {s.RequestedQuantity}, khả dụng {s.AvailableQuantity}.");
                            throw new BadRequestException($"Kiểm tra tồn kho thất bại:\n{string.Join("\n", errors)}");
                        }
                    }
                }
            }
            catch (JsonException) { /* invalid json, ignore and keep going if not critical */ }
        }

        // 3. Update activity fields
        activity.Step = request.Step ?? activity.Step;
        activity.ActivityType = request.ActivityType ?? activity.ActivityType;
        activity.Description = request.Description ?? activity.Description;
        activity.Target = request.Target ?? activity.Target;
        activity.Items = request.Items ?? activity.Items;
        activity.TargetLatitude = request.TargetLatitude ?? activity.TargetLatitude;
        activity.TargetLongitude = request.TargetLongitude ?? activity.TargetLongitude;
        if (request.AssemblyPointId.HasValue)
        {
            var assemblyPoint = await _assemblyPointRepository.GetByIdAsync(request.AssemblyPointId.Value, cancellationToken)
                ?? throw new BadRequestException($"Khong tim thay diem tap ket #{request.AssemblyPointId.Value}.");

            if (assemblyPoint.Location is null)
                throw new BadRequestException($"Diem tap ket #{request.AssemblyPointId.Value} chua co toa do hop le.");

            activity.AssemblyPointId = assemblyPoint.Id;
            activity.AssemblyPointName = assemblyPoint.Name;
            activity.AssemblyPointLatitude = assemblyPoint.Location.Latitude;
            activity.AssemblyPointLongitude = assemblyPoint.Location.Longitude;
        }

        await _activityRepository.UpdateAsync(activity, cancellationToken);
        await _unitOfWork.SaveAsync();

        // 4. Reserve NEW supplies
        if (newDepotId.HasValue && nextItems is { Count: > 0 })
        {
            var itemsToReserve = nextItems
                .Where(i => i.ItemId.HasValue && i.Quantity > 0)
                .Select(i => (ItemModelId: i.ItemId!.Value, Quantity: i.Quantity + (i.BufferQuantity ?? 0)))
                .ToList();

            if (itemsToReserve.Count > 0)
            {
                try
                {
                    var reservationResult = await _depotInventoryRepository.ReserveSuppliesAsync(newDepotId.Value, itemsToReserve, cancellationToken);
                    await MissionSupplyExecutionSnapshotHelper.SyncReservationSnapshotAsync(
                        activity,
                        reservationResult,
                        _activityRepository,
                        _logger,
                        cancellationToken);
                    await _unitOfWork.SaveAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể đặt trước vật phẩm mới cho activity #{ActivityId}", activity.Id);
                }
            }
        }
        else
        {
            await MissionSupplyExecutionSnapshotHelper.RebuildExpectedReturnUnitsAsync(
                activity,
                _activityRepository,
                _logger,
                cancellationToken);
            await _unitOfWork.SaveAsync();
        }

        return new UpdateMissionActivityResponse
        {
            ActivityId = activity.Id,
            MissionId = activity.MissionId ?? 0,
            Step = activity.Step,
            ActivityType = activity.ActivityType,
            Description = activity.Description,
            Status = activity.Status.ToString(),
            AssemblyPointId = activity.AssemblyPointId,
            AssemblyPointName = activity.AssemblyPointName,
            AssemblyPointLatitude = activity.AssemblyPointLatitude,
            AssemblyPointLongitude = activity.AssemblyPointLongitude,
            SuppliesToCollect = string.IsNullOrWhiteSpace(activity.Items)
                ? null
                : JsonSerializer.Deserialize<List<SupplyToCollectDto>>(activity.Items, _jsonOpts)
        };
    }

    private static bool CanUpdateOngoingReturnAssemblyPoint(
        MissionActivityModel activity,
        UpdateMissionActivityCommand request)
    {
        return string.Equals(activity.ActivityType, ReturnAssemblyPointActivityType, StringComparison.OrdinalIgnoreCase)
            && activity.Status == MissionActivityStatus.OnGoing
            && !request.Step.HasValue
            && request.ActivityType is null
            && request.Target is null
            && request.Items is null
            && !request.TargetLatitude.HasValue
            && !request.TargetLongitude.HasValue;
    }
}
