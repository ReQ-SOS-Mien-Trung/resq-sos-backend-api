using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportInventory;

public class ImportReliefItemsCommandHandler(
    IItemCategoryRepository categoryRepository,
    IOrganizationReliefRepository organizationReliefRepository,
    ILogger<ImportReliefItemsCommandHandler> logger)
    : IRequestHandler<ImportReliefItemsCommand, ImportReliefItemsResponse>
{
    private readonly IItemCategoryRepository _categoryRepository = categoryRepository;
    private readonly IOrganizationReliefRepository _organizationReliefRepository = organizationReliefRepository;
    private readonly ILogger<ImportReliefItemsCommandHandler> _logger = logger;

    public async Task<ImportReliefItemsResponse> Handle(ImportReliefItemsCommand request, CancellationToken cancellationToken)
    {
        var response = new ImportReliefItemsResponse();

        // Pre-fetch all categories into memory for efficient matching
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);

        foreach (var item in request.Items)
        {
            try
            {
                // 1. Find category by categoryCode mapping
                var category = categories.FirstOrDefault(c => string.Equals(c.Code.ToString(), item.CategoryCode, StringComparison.OrdinalIgnoreCase));
                
                if (category == null)
                {
                    response.Failed++;
                    response.Errors.Add(new ImportErrorDto { Row = item.Row, Message = $"Không tìm thấy danh mục vật phẩm có mã: {item.CategoryCode}" });
                    continue;
                }

                // 2. Use the repository to get or create the item (Domain/DB logic is hidden from Handler)
                var reliefItemId = await _organizationReliefRepository.GetOrCreateReliefItemAsync(
                    category.Id,
                    item.ItemName,
                    item.Unit,
                    item.ItemType,
                    item.TargetGroup,
                    cancellationToken
                );

                // 3. Add the inventory record for the organization
                await _organizationReliefRepository.AddOrganizationReliefItemAsync(
                    request.OrganizationId,
                    reliefItemId,
                    item.ReceivedDate,
                    item.ExpiredDate,
                    item.Notes,
                    cancellationToken
                );

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
