using MediatR;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Queries.GeneratePurchaseImportTemplate;

public class GeneratePurchaseImportTemplateQueryHandler(
    IItemCategoryRepository categoryRepository,
    IItemModelMetadataRepository itemModelRepository,
    IExcelExportService excelExportService)
    : IRequestHandler<GeneratePurchaseImportTemplateQuery, GeneratePurchaseImportTemplateResult>
{
    private readonly IItemCategoryRepository _categoryRepository = categoryRepository;
    private readonly IItemModelMetadataRepository _itemModelRepository = itemModelRepository;
    private readonly IExcelExportService _excelExportService = excelExportService;

    public async Task<GeneratePurchaseImportTemplateResult> Handle(
        GeneratePurchaseImportTemplateQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Load all categories
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);

        var categoryInfos = categories
            .Select(c => new DonationImportCategoryInfo(c.Id, c.Code.ToString(), c.Name))
            .ToList();

        // 2. Load all item models with target groups (resolved in Infrastructure)
        var itemInfos = await _itemModelRepository.GetAllForDonationTemplateAsync(cancellationToken);

        // 3. Generate Excel template
        var fileBytes = _excelExportService.GeneratePurchaseImportTemplate(categoryInfos, itemInfos);

        return new GeneratePurchaseImportTemplateResult
        {
            FileContent = fileBytes,
            FileName = $"Mau_Nhap_Kho_Mua_Sam_{DateTime.UtcNow.AddHours(7):yyyyMMdd}.xlsx"
        };
    }
}
