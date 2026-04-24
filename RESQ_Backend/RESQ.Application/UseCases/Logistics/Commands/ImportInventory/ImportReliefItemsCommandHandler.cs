using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Enum.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportInventory;

public class ImportReliefItemsCommandHandler(
    IItemCategoryRepository categoryRepository,
    IOrganizationReliefRepository organizationReliefRepository,
    IOrganizationMetadataRepository organizationMetadataRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository,
    IItemModelMetadataRepository itemModelMetadataRepository,
    IUnitOfWork unitOfWork,
    ILogger<ImportReliefItemsCommandHandler> logger,
    IManagerDepotAccessService managerDepotAccessService,
    IUserRepository userRepository,
    IFirebaseService firebaseService)
    : IRequestHandler<ImportReliefItemsCommand, ImportReliefItemsResponse>
{
    private readonly IItemCategoryRepository _categoryRepository = categoryRepository;
    private readonly IOrganizationReliefRepository _organizationReliefRepository = organizationReliefRepository;
    private readonly IOrganizationMetadataRepository _organizationMetadataRepository = organizationMetadataRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<ImportReliefItemsCommandHandler> _logger = logger;
    private readonly IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IFirebaseService _firebaseService = firebaseService;

    public async Task<ImportReliefItemsResponse> Handle(ImportReliefItemsCommand request, CancellationToken cancellationToken)
    {
        var response = new ImportReliefItemsResponse();

        // 1. Get the active depot managed by this user
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động. Không thể nhập hàng.");
        var depotStatus = await _depotRepository.GetStatusByIdAsync(depotId, cancellationToken);
        if (depotStatus is DepotStatus.Unavailable or DepotStatus.Closing or DepotStatus.Closed)
            throw new ConflictException("Kho ngưng hoạt động hoặc đã đóng. Không thể nhập hàng vào kho này.");

        // 3. Pre-fetch all categories into memory for efficient matching
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);
        var categoriesByCode = categories
            .ToDictionary(c => c.Code.ToString(), c => c, StringComparer.OrdinalIgnoreCase);
        var batchNote = NormalizeNote(request.BatchNote);

        // 3b. Batch-fetch existing item models for Path A rows (ItemModelId provided)
        var itemModelIds = request.Items
            .Where(x => x.ItemModelId.HasValue)
            .Select(x => x.ItemModelId!.Value)
            .Distinct()
            .ToList();

        Dictionary<int, ItemModelRecord> existingItemModels;
        if (itemModelIds.Count > 0)
        {
            existingItemModels = await _itemModelMetadataRepository.GetByIdsAsync(itemModelIds, cancellationToken);

            // Detect missing IDs early - log once at batch level
            var missingIds = itemModelIds.Where(id => !existingItemModels.ContainsKey(id)).ToList();
            if (missingIds.Count > 0)
            {
                _logger.LogWarning("Donation import: {MissingCount} ItemModelId(s) not found in DB: {MissingIds}",
                    missingIds.Count, missingIds);
            }
        }
        else
        {
            existingItemModels = new Dictionary<int, ItemModelRecord>();
        }

        // 4. Validate all items and prepare domain models (dual-path)
        var validItems = new List<(ImportReliefItemDto dto, ItemModelRecord reliefItem)>();
        var rowErrors = new Dictionary<int, HashSet<string>>();

        foreach (var item in request.Items)
        {
            try
            {
                ItemModelRecord? resolvedRecord = null;

                if (item.ItemModelId.HasValue)
                {
                    // -- Path A: Existing item by ID --
                    if (!existingItemModels.TryGetValue(item.ItemModelId.Value, out var existingRecord))
                    {
                        AddRowError(rowErrors, item.Row, $"Không tìm thấy item model có ID: {item.ItemModelId.Value}");
                        continue;
                    }
                    resolvedRecord = existingRecord;
                }
                else
                {
                    // -- Path B: Create new item from metadata --
                    var normalizedName = item.ItemName?.Trim();
                    var normalizedUnit = item.Unit?.Trim();
                    var normalizedItemType = item.ItemType?.Trim();
                    var normalizedCategoryCode = item.CategoryCode?.Trim();

                    // Validate metadata before calling Create()
                    if (string.IsNullOrWhiteSpace(normalizedName))
                    {
                        AddRowError(rowErrors, item.Row, "Tên vật phẩm không được để trống");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(normalizedCategoryCode))
                    {
                        AddRowError(rowErrors, item.Row, "Mã danh mục không được để trống");
                        continue;
                    }

                    var category = categoriesByCode.GetValueOrDefault(normalizedCategoryCode!);

                    if (category == null)
                    {
                        AddRowError(rowErrors, item.Row, $"Không tìm thấy danh mục vật phẩm có mã: {item.CategoryCode}");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(normalizedUnit))
                    {
                        AddRowError(rowErrors, item.Row, "Đơn vị tính không được để trống");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(normalizedItemType))
                    {
                        AddRowError(rowErrors, item.Row, "Loại vật phẩm không được để trống");
                        continue;
                    }

                    var targetGroups = item.TargetGroups?
                        .Where(g => !string.IsNullOrWhiteSpace(g))
                        .Select(g => g.Trim())
                        .ToList() ?? new();

                    if (targetGroups.Count == 0)
                    {
                        AddRowError(rowErrors, item.Row, "Nhóm đối tượng không được để trống");
                        continue;
                    }

                    try
                    {
                        resolvedRecord = ItemModelRecord.Create(
                            category.Id,
                            normalizedName,
                            normalizedUnit,
                            normalizedItemType,
                            targetGroups,
                            volumePerUnit: item.VolumePerUnit ?? 0,
                            weightPerUnit: item.WeightPerUnit ?? 0,
                            description: item.Description);
                        resolvedRecord.ImageUrl = string.IsNullOrWhiteSpace(item.ImageUrl) ? null : item.ImageUrl.Trim();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unexpected error creating ItemModelRecord for row {Row}", item.Row);
                        AddRowError(rowErrors, item.Row, "Lỗi hệ thống khi tạo item model");
                        continue;
                    }
                }

                validItems.Add((item, resolvedRecord));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing item at row {Row}", item.Row);
                AddRowError(rowErrors, item.Row, ex.Message);
            }
        }

        // Flatten row errors into sorted error list
        var errors = rowErrors
            .OrderBy(kv => kv.Key)
            .Select(kv => new ImportErrorDto { Row = kv.Key, Message = $"[Dòng {kv.Key}] {string.Join("; ", kv.Value)}" })
            .ToList();

        response.Failed = errors.Count;
        response.Errors = errors;

        if (validItems.Count == 0)
        {
            return response;
        }

        // Sort resolved items by row for predictable output
        validItems = validItems.OrderBy(x => x.dto.Row).ToList();

        _logger.LogInformation(
            "Donation import: {ValidCount} valid items, {ErrorCount} errors out of {TotalCount} total rows",
            validItems.Count, errors.Count, request.Items.Count);

        // 5. Execute all bulk operations within a transaction to ensure atomicity
        try
        {
            var importedCount = 0;

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                int organizationId;
                if (request.OrganizationId.HasValue)
                {
                    var existingOrganization = await _organizationMetadataRepository.GetByIdAsync(request.OrganizationId.Value, cancellationToken);
                    if (existingOrganization == null)
                    {
                        throw new BadRequestException($"Không tìm thấy tổ chức có ID: {request.OrganizationId.Value}");
                    }

                    organizationId = existingOrganization.Id;
                }
                else if (!string.IsNullOrWhiteSpace(request.OrganizationName))
                {
                    var existingOrganization = await _organizationMetadataRepository.GetByNameAsync(request.OrganizationName, cancellationToken);
                    if (existingOrganization != null)
                    {
                        organizationId = existingOrganization.Id;
                    }
                    else
                    {
                        await _organizationMetadataRepository.CreateAsync(request.OrganizationName.Trim(), cancellationToken);
                        await _unitOfWork.SaveAsync();

                        var createdOrganization = await _organizationMetadataRepository.GetByNameAsync(request.OrganizationName.Trim(), cancellationToken);
                        if (createdOrganization == null)
                        {
                            throw new CreateFailedException("Không thể tạo tổ chức tiếp nhận cho lô nhập cứu trợ.");
                        }

                        organizationId = createdOrganization.Id;
                    }
                }
                else
                {
                    throw new BadRequestException("Phải cung cấp ID tổ chức hoặc tên tổ chức.");
                }

                var newItemModels = validItems
                    .Where(x => !x.dto.ItemModelId.HasValue)
                    .Select(x => x.reliefItem)
                    .ToList();

                List<ItemModelRecord> createdItems;
                if (newItemModels.Count > 0)
                {
                    await _organizationReliefRepository.CreateReliefItemsBulkAsync(newItemModels, cancellationToken);
                    await _unitOfWork.SaveAsync();
                    createdItems = await _organizationReliefRepository.GetReliefItemsBulkByDefinitionAsync(newItemModels, cancellationToken);
                }
                else
                {
                    createdItems = new List<ItemModelRecord>();
                }

                var createdIndex = 0;
                var donationModels = new List<OrganizationReliefItemModel>(validItems.Count);

                foreach (var (dto, reliefItem) in validItems)
                {
                    var resolvedItemModelId = dto.ItemModelId ?? createdItems[createdIndex++].Id;
                    var receivedDateUtc = dto.ReceivedDate.HasValue
                        ? DateTime.SpecifyKind(dto.ReceivedDate.Value, DateTimeKind.Utc)
                        : (DateTime?)null;
                    var expiredDateUtc = dto.ExpiredDate.HasValue
                        ? DateTime.SpecifyKind(dto.ExpiredDate.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
                        : (DateTime?)null;

                    donationModels.Add(OrganizationReliefItemModel.Create(
                        organizationId,
                        resolvedItemModelId,
                        dto.Quantity,
                        reliefItem.ItemType,
                        receivedDateUtc,
                        expiredDateUtc,
                        batchNote,
                        request.UserId,
                        depotId,
                        batchNote,
                        null));
                }

                await _organizationReliefRepository.AddOrganizationReliefItemsBulkAsync(donationModels, cancellationToken);
                await _unitOfWork.SaveAsync();
                importedCount = donationModels.Count;
            });

            response.Imported = importedCount;

            // Gửi thông báo đến toàn bộ Coordinator sau khi commit thành công
            try
            {
                var depot = await _depotRepository.GetByIdAsync(depotId, cancellationToken);
                var depotName = depot?.Name ?? $"Kho #{depotId}";
                var coordinatorIds = await _userRepository.GetActiveCoordinatorUserIdsAsync(cancellationToken);
                var notifTitle = "Thông báo nhập hàng mới";
                var notifBody = $"Kho {depotName} vừa hoàn tất nhập hàng quyên góp ({response.Imported} mặt hàng). Đề nghị kiểm tra và xác nhận tình trạng tồn kho.";
                var notifData = new Dictionary<string, string>
                {
                    ["depotId"] = depotId.ToString(),
                    ["type"] = "depot_relief_imported"
                };
                foreach (var coordinatorId in coordinatorIds)
                {
                    _ = _firebaseService.SendNotificationToUserAsync(
                        coordinatorId,
                        notifTitle,
                        notifBody,
                        "depot_relief_imported",
                        notifData,
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify coordinators after relief import | DepotId={DepotId}", depotId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi trong quá trình nhập hàng hàng loạt. Tất cả thay đổi đã được hoàn tác.");
            throw new CreateFailedException("Lỗi trong quá trình nhập hàng. Vui lòng thử lại.");
        }

        return response;
    }

    /// <summary>
    /// Adds an error message for a specific row. Deduplicates via HashSet.
    /// </summary>
    private static void AddRowError(Dictionary<int, HashSet<string>> rowErrors, int row, string message)
    {
        if (!rowErrors.TryGetValue(row, out var messages))
        {
            messages = new HashSet<string>(StringComparer.Ordinal);
            rowErrors[row] = messages;
        }
        messages.Add(message);
    }

    private static string? NormalizeNote(string? note)
        => string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}
