using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Base;
using RESQ.Infrastructure.Entities.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportInventory;

public class ImportReliefItemsCommandHandler(
    IUnitOfWork unitOfWork,
    ILogger<ImportReliefItemsCommandHandler> logger)
    : IRequestHandler<ImportReliefItemsCommand, ImportReliefItemsResponse>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<ImportReliefItemsCommandHandler> _logger = logger;

    public async Task<ImportReliefItemsResponse> Handle(ImportReliefItemsCommand request, CancellationToken cancellationToken)
    {
        var response = new ImportReliefItemsResponse();

        var categoryRepo = _unitOfWork.GetRepository<ItemCategory>();
        var reliefItemRepo = _unitOfWork.GetRepository<ReliefItem>();
        var orgReliefItemRepo = _unitOfWork.GetRepository<OrganizationReliefItem>();

        // Pre-fetch all categories into memory for efficient matching
        var categories = await categoryRepo.GetAllByPropertyAsync(null, null);

        foreach (var item in request.Items)
        {
            try
            {
                // 1. Find category by categoryCode
                var category = categories.FirstOrDefault(c => string.Equals(c.Code, item.CategoryCode, StringComparison.OrdinalIgnoreCase));
                
                if (category == null)
                {
                    response.Failed++;
                    response.Errors.Add(new ImportErrorDto { Row = item.Row, Message = $"Không tìm thấy danh mục vật phẩm có mã: {item.CategoryCode}" });
                    continue;
                }

                // 2. Find relief_item by name + category_id
                var reliefItem = await reliefItemRepo.GetByPropertyAsync(
                    r => r.Name == item.ItemName && r.CategoryId == category.Id, 
                    tracked: true);

                // 3. If not exists -> create relief_item
                if (reliefItem == null)
                {
                    reliefItem = new ReliefItem
                    {
                        CategoryId = category.Id,
                        Name = item.ItemName,
                        Unit = item.Unit,
                        ItemType = item.ItemType,
                        TargetGroup = item.TargetGroup,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    
                    await reliefItemRepo.AddAsync(reliefItem);
                    await _unitOfWork.SaveAsync(); // Save to generate ID for the navigation property
                }

                // 4. Insert organization_relief_item
                var orgReliefItem = new OrganizationReliefItem
                {
                    OrganizationId = request.OrganizationId,
                    ReliefItemId = reliefItem.Id,
                    ReceivedDate = item.ReceivedDate,
                    ExpiredDate = item.ExpiredDate,
                    Notes = item.Notes,
                    CreatedAt = DateTime.UtcNow
                };

                await orgReliefItemRepo.AddAsync(orgReliefItem);
                await _unitOfWork.SaveAsync();

                response.Imported++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi nhập vật phẩm dòng {Row}", item.Row);
                
                response.Failed++;
                response.Errors.Add(new ImportErrorDto { Row = item.Row, Message = ex.Message });
            }
        }

        return response;
    }
}