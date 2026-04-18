using MediatR;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Logistics.Queries.GenerateDonationImportTemplate;

public class GenerateDonationImportTemplateQueryHandler(
    IItemCategoryRepository categoryRepository,
    IItemModelMetadataRepository itemModelRepository,
    IExcelExportService excelExportService)
    : IRequestHandler<GenerateDonationImportTemplateQuery, GenerateDonationImportTemplateResult>
{
    private readonly IItemCategoryRepository _categoryRepository = categoryRepository;
    private readonly IItemModelMetadataRepository _itemModelRepository = itemModelRepository;
    private readonly IExcelExportService _excelExportService = excelExportService;

    public async Task<GenerateDonationImportTemplateResult> Handle(
        GenerateDonationImportTemplateQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Load all categories
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);

        var categoryInfos = categories
            .Select(c => new DonationImportCategoryInfo(c.Id, c.Code.ToString(), c.Name))
            .ToList();

        // 2. Load all item models with target groups (resolved in Infrastructure)
        var itemInfos = await _itemModelRepository.GetAllForDonationTemplateAsync(cancellationToken);

        // 2b. Load target groups directly from database for dropdown source
        var targetGroups = await _itemModelRepository.GetAllTargetGroupsForTemplateAsync(cancellationToken);

        // 3. Generate Excel template
        var fileBytes = _excelExportService.GenerateDonationImportTemplate(categoryInfos, itemInfos, targetGroups);

        return new GenerateDonationImportTemplateResult
        {
            FileContent = fileBytes,
            FileName = $"Mau_Nhap_Kho_Tu_Thien_{DateTime.UtcNow.AddHours(7):yyyyMMdd}.xlsx"
        };
    }
}
