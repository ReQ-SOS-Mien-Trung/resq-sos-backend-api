using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Logistics;
using RESQ.Application.Exceptions;
using RESQ.Application.Extensions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Exceptions.Logistics;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.CreateSupplyRequest;

public class CreateSupplyRequestCommandHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository,
    IItemModelMetadataRepository itemModelMetadataRepository,
    ISupplyRequestRepository supplyRequestRepository,
    ISupplyRequestPriorityConfigRepository supplyRequestPriorityConfigRepository,
    IFirebaseService firebaseService,
    IUnitOfWork unitOfWork,
    ILogger<CreateSupplyRequestCommandHandler> logger)
    : IRequestHandler<CreateSupplyRequestCommand, CreateSupplyRequestResponse>
{
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly ISupplyRequestRepository _supplyRequestRepository = supplyRequestRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly ISupplyRequestPriorityConfigRepository _supplyRequestPriorityConfigRepository = supplyRequestPriorityConfigRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IFirebaseService _firebaseService = firebaseService;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly ILogger<CreateSupplyRequestCommandHandler> _logger = logger;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;

    public async Task<CreateSupplyRequestResponse> Handle(CreateSupplyRequestCommand request, CancellationToken cancellationToken)
    {
        // 1. L?y kho c?a manager dang dang nh?p
        var requestingDepotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.RequestingUserId, request.DepotId, cancellationToken);
        if (requestingDepotId == null)
            throw new BadRequestException("T�i kho?n hi?n t?i kh�ng du?c ch? d?nh qu?n l� b?t k? kho n�o dang ho?t d?ng.");

        var requestingDepotStatus = await _depotRepository.GetStatusByIdAsync(requestingDepotId.Value, cancellationToken);
        if (requestingDepotStatus is DepotStatus.Unavailable or DepotStatus.Closing or DepotStatus.Closed)
            throw new ConflictException("Kho c?a b?n ngung ho?t d?ng, dang d�ng ho?c d� d�ng. Kh�ng th? t?o y�u c?u ti?p t?.");

        var requestingDepot = await _depotRepository.GetByIdAsync(requestingDepotId.Value, cancellationToken)
            ?? throw new NotFoundException($"Kh�ng t�m th?y kho y�u c?u #{requestingDepotId.Value}.");

        // 2. Validate kh�ng c� group n�o tr? v? ch�nh kho c?a manager
        var selfRequest = request.Requests.FirstOrDefault(r => r.SourceDepotId == requestingDepotId.Value);
        if (selfRequest != null)
            throw new InvalidSupplyRequestException("Kh�ng th? t?o y�u c?u cung c?p t? ch�nh kho c?a b?n.");

        // 3. Ki?m tra c�c kho ngu?n kh�ng dang d�ng
        foreach (var group in request.Requests)
        {
            var sourceStatus = await _depotRepository.GetStatusByIdAsync(group.SourceDepotId, cancellationToken);
            if (sourceStatus is DepotStatus.Unavailable or DepotStatus.Closing or DepotStatus.Closed)
                throw new ConflictException($"Kho ngu?n #{group.SourceDepotId} ngung ho?t d?ng, dang d�ng ho?c d� d�ng. Kh�ng th? g?i y�u c?u ti?p t? d?n kho n�y.");
        }

        // 4. Validate kho y�u c?u c�n d? s?c ch?a d? nh?n to�n b? lu?ng h�ng trong request
        var requestedItems = request.Requests
            .SelectMany(group => group.Items)
            .GroupBy(item => item.ItemModelId)
            .Select(group => new
            {
                ItemModelId = group.Key,
                Quantity = group.Sum(item => item.Quantity)
            })
            .ToList();

        var itemModelMap = await _itemModelMetadataRepository.GetByIdsAsync(
            requestedItems.Select(x => x.ItemModelId).ToList(),
            cancellationToken);

        var missingItemModelIds = requestedItems
            .Where(x => !itemModelMap.ContainsKey(x.ItemModelId))
            .Select(x => x.ItemModelId)
            .ToList();

        if (missingItemModelIds.Count > 0)
            throw new NotFoundException($"Kh�ng t�m th?y metadata v?t ph?m cho c�c itemModelId: {string.Join(", ", missingItemModelIds)}.");

        var totalRequestedVolume = requestedItems.Sum(x =>
            x.Quantity * itemModelMap[x.ItemModelId].VolumePerUnit);

        var totalRequestedWeight = requestedItems.Sum(x =>
            x.Quantity * itemModelMap[x.ItemModelId].WeightPerUnit);

        var remainingVolumeCapacity = requestingDepot.Capacity - requestingDepot.CurrentUtilization;
        var remainingWeightCapacity = requestingDepot.WeightCapacity - requestingDepot.CurrentWeightUtilization;

        if (totalRequestedVolume > remainingVolumeCapacity || totalRequestedWeight > remainingWeightCapacity)
        {
            var reasons = new List<string>();
            if (totalRequestedVolume > remainingVolumeCapacity)
            {
                reasons.Add(
                    $"th? t�ch c?n {totalRequestedVolume:N2} dm� nhung kho ch? c�n {remainingVolumeCapacity:N2} dm�");
            }

            if (totalRequestedWeight > remainingWeightCapacity)
            {
                reasons.Add(
                    $"c�n n?ng c?n {totalRequestedWeight:N2} kg nhung kho ch? c�n {remainingWeightCapacity:N2} kg");
            }

            throw new ConflictException(
                $"Kho y�u c?u kh�ng d? s?c ch?a d? nh?n to�n b? v?t ph?m trong phi?u ti?p t?: {string.Join("; ", reasons)}.");
        }

        // 5. X? l� t?ng kho ngu?n trong transaction
        var config = await _supplyRequestPriorityConfigRepository.GetAsync(cancellationToken);
        var timing = config == null
            ? SupplyRequestPriorityPolicy.DefaultTiming
            : new SupplyRequestPriorityTiming(config.UrgentMinutes, config.HighMinutes, config.MediumMinutes);

        var createdRequests = new List<CreatedSupplyRequestDto>();

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            foreach (var group in request.Requests)
            {
                var items = group.Items
                    .Select(i => (i.ItemModelId, i.Quantity))
                    .ToList();

                var autoRejectAt = SupplyRequestPriorityPolicy.ResolveAutoRejectAt(
                    DateTime.UtcNow,
                    group.PriorityLevel,
                    timing);

                var supplyRequestId = await _supplyRequestRepository.CreateAsync(
                    requestingDepotId.Value,
                    group.SourceDepotId,
                    items,
                    group.PriorityLevel,
                    autoRejectAt,
                    group.Note,
                    request.RequestingUserId,
                    cancellationToken);

                createdRequests.Add(new CreatedSupplyRequestDto
                {
                    SupplyRequestId  = supplyRequestId,
                    SourceDepotId    = group.SourceDepotId,
                    ResponseDeadline = autoRejectAt.ToVietnamOffset()
                });
            }
        });

        // 6. G?i notification cho manager c?a kho ngu?n
        foreach (var created in createdRequests)
        {
            var sourceManagerUserId = await _supplyRequestRepository.GetActiveManagerUserIdByDepotIdAsync(created.SourceDepotId, cancellationToken);
            if (sourceManagerUserId.HasValue)
            {
                var createdGroup = request.Requests.First(x => x.SourceDepotId == created.SourceDepotId);
                var isUrgent = createdGroup.PriorityLevel == SupplyRequestPriorityLevel.Urgent;
                var notificationTitle = isUrgent ? "Y�u c?u ti?p t? kh?n c?p" : "Y�u c?u cung c?p v?t ph?m m?i";
                var notificationBody = isUrgent
                    ? $"Y�u c?u ti?p t? s? {created.SupplyRequestId} d� v�o m?c kh?n c?p. Vui l�ng uu ti�n x? l� ngay."
                    : $"Kho c?a b?n v?a nh?n du?c y�u c?u cung c?p v?t ph?m s? {created.SupplyRequestId}. Vui l�ng ki?m tra v� x? l�.";
                var notificationType = isUrgent ? "supply_request_urgent" : "supply_request";

                await _firebaseService.SendNotificationToUserAsync(
                    sourceManagerUserId.Value,
                    notificationTitle,
                    notificationBody,
                    notificationType,
                    cancellationToken);

                if (isUrgent)
                    await _supplyRequestRepository.MarkUrgentEscalationNotifiedAsync(created.SupplyRequestId, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Kho ngu?n {SourceDepotId} kh�ng c� manager active. Kh�ng th? g?i th�ng b�o.", created.SourceDepotId);
            }
        }

        return new CreateSupplyRequestResponse
        {
            CreatedRequests = createdRequests,
            Message         = $"�� t?o {createdRequests.Count} y�u c?u cung c?p v?t ph?m th�nh c�ng.",
            ServerTime      = DateTime.UtcNow.ToVietnamOffset()
        };
    }
}

