using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Logistics;
using RESQ.Domain.Entities.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportInventory;

public class ImportReliefItemsCommandHandler(
    IItemCategoryRepository categoryRepository,
    IOrganizationReliefRepository organizationReliefRepository,
    IOrganizationMetadataRepository organizationMetadataRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IUnitOfWork unitOfWork,
    ILogger<ImportReliefItemsCommandHandler> logger)
    : IRequestHandler<ImportReliefItemsCommand, ImportReliefItemsResponse>
{
    private readonly IItemCategoryRepository _categoryRepository = categoryRepository;
    private readonly IOrganizationReliefRepository _organizationReliefRepository = organizationReliefRepository;
    private readonly IOrganizationMetadataRepository _organizationMetadataRepository = organizationMetadataRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<ImportReliefItemsCommandHandler> _logger = logger;

    public async Task<ImportReliefItemsResponse> Handle(ImportReliefItemsCommand request, CancellationToken cancellationToken)
    {
        var response = new ImportReliefItemsResponse();

        // 1. Get the active depot managed by this user
        var depotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken);
        if (depotId == null)
        {
            throw new BadRequestException("Tài khoản hiện tại không được chỉ định quản lý bất kỳ kho nào đang hoạt động. Không thể nhập hàng.");
        }

        // 2. Resolve organization ID
        int organizationId;
        if (request.OrganizationId.HasValue)
        {
            // Use existing organization ID
            var existingOrg = await _organizationMetadataRepository.GetByIdAsync(request.OrganizationId.Value, cancellationToken);
            if (existingOrg == null)
            {
                throw new BadRequestException($"Không tìm thấy tổ chức có ID: {request.OrganizationId.Value}");
            }
            organizationId = existingOrg.Id;
        }
        else if (!string.IsNullOrEmpty(request.OrganizationName))
        {
            // Find existing organization by name or create new one
            var existingOrg = await _organizationMetadataRepository.GetByNameAsync(request.OrganizationName, cancellationToken);
            if (existingOrg != null)
            {
                organizationId = existingOrg.Id;
            }
            else
            {
                var newOrg = await _organizationMetadataRepository.CreateAsync(request.OrganizationName, cancellationToken);
                organizationId = newOrg.Id;
            }
        }
        else
        {
            throw new BadRequestException("Phải cung cấp ID tổ chức hoặc tên tổ chức.");
        }

        // 3. Pre-fetch all categories into memory for efficient matching
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);

        // 4. Validate all items and prepare domain models
        var validItems = new List<(ImportReliefItemDto dto, ItemModelRecord reliefItem, OrganizationReliefItemModel donation)>();
        var errors = new List<ImportErrorDto>();

        foreach (var item in request.Items)
        {
            try
            {
                var category = categories.FirstOrDefault(c => string.Equals(c.Code.ToString(), item.CategoryCode, StringComparison.OrdinalIgnoreCase));
                
                if (category == null)
                {
                    errors.Add(new ImportErrorDto { Row = item.Row, Message = $"Không tìm thấy danh mục vật phẩm có mã: {item.CategoryCode}" });
                    continue;
                }

                // Domain Orchestration: Create Relief Item Model
                var reliefItemModel = ItemModelRecord.Create(
                    category.Id, 
                    item.ItemName, 
                    item.Unit, 
                    item.ItemType, 
                    item.TargetGroup);

                // Normalize dates to UTC before persisting
                var receivedDateUtc = item.ReceivedDate.HasValue
                    ? DateTime.SpecifyKind(item.ReceivedDate.Value, DateTimeKind.Utc)
                    : (DateTime?)null;
                var expiredDateUtc = item.ExpiredDate.HasValue
                    ? DateTime.SpecifyKind(item.ExpiredDate.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
                    : (DateTime?)null;

                // Domain Orchestration: Create Organization Donation Receipt Model
                var donationModel = OrganizationReliefItemModel.Create(
                    organizationId,
                    0, // Will be set after relief items are created
                    item.Quantity,
                    item.ItemType,
                    receivedDateUtc,
                    expiredDateUtc,
                    item.Notes,
                    request.UserId,
                    depotId.Value
                );

                validItems.Add((item, reliefItemModel, donationModel));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi validate vật phẩm dòng {Row}", item.Row);
                errors.Add(new ImportErrorDto { Row = item.Row, Message = ex.Message });
            }
        }

        response.Failed = errors.Count;
        response.Errors = errors;

        if (validItems.Count == 0)
        {
            return response;
        }

        // 5. Execute all bulk operations within a transaction to ensure atomicity
        try
        {
            // Bulk create/get relief items
            var reliefItemModels = validItems.Select(x => x.reliefItem).ToList();
            var savedReliefItems = await _organizationReliefRepository.GetOrCreateReliefItemsBulkAsync(reliefItemModels, cancellationToken);

            // Map relief item IDs back to donation models
            var donationModels = new List<OrganizationReliefItemModel>();
            for (int i = 0; i < validItems.Count; i++)
            {
                var (dto, reliefItem, donation) = validItems[i];
                var savedReliefItem = savedReliefItems.FirstOrDefault(r => 
                    r.Name == reliefItem.Name && 
                    r.CategoryId == reliefItem.CategoryId &&
                    r.Unit == reliefItem.Unit &&
                    r.ItemType == reliefItem.ItemType &&
                    r.TargetGroup == reliefItem.TargetGroup);

                if (savedReliefItem != null)
                {
                    // Update donation model with correct relief item ID
                    var updatedDonation = OrganizationReliefItemModel.Create(
                        organizationId,
                        savedReliefItem.Id,
                        donation.Quantity,
                        donation.ItemType,
                        donation.ReceivedDate,
                        donation.ExpiredDate,
                        donation.Notes,
                        donation.ReceivedBy,
                        donation.ReceivedAt
                    );
                    donationModels.Add(updatedDonation);
                }
            }

            // Bulk insert donation records, inventory updates, and logs
            await _organizationReliefRepository.AddOrganizationReliefItemsBulkAsync(donationModels, cancellationToken);

            // Save all changes within a transaction for atomicity
            await _unitOfWork.SaveChangesWithTransactionAsync();

            response.Imported = donationModels.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi trong quá trình nhập hàng hàng loạt. Tất cả thay đổi đã được hoàn tác.");
            throw new CreateFailedException("Lỗi trong quá trình nhập hàng. Vui lòng thử lại.");
        }

        return response;
    }
}
