using MediatR;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Application.UseCases.Finance.Queries.GenerateFundingRequestTemplate;

public class GenerateFundingRequestTemplateQueryHandler(
    IItemCategoryRepository categoryRepository,
    IItemModelMetadataRepository itemModelRepository,
    IExcelExportService excelExportService)
    : IRequestHandler<GenerateFundingRequestTemplateQuery, GenerateFundingRequestTemplateResult>
{
    private readonly IItemCategoryRepository _categoryRepository = categoryRepository;
    private readonly IItemModelMetadataRepository _itemModelRepository = itemModelRepository;
    private readonly IExcelExportService _excelExportService = excelExportService;

    public async Task<GenerateFundingRequestTemplateResult> Handle(
        GenerateFundingRequestTemplateQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Load all categories
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);

        var categoryInfos = categories
            .Select(c => new DonationImportCategoryInfo(c.Id, c.Code.ToString(), c.Name))
            .ToList();

        // 2. Load all item models with target groups
        var itemInfos = await _itemModelRepository.GetAllForDonationTemplateAsync(cancellationToken);

        // 3. Load target groups for dropdown source
        var targetGroups = await _itemModelRepository.GetAllTargetGroupsForTemplateAsync(cancellationToken);

        // 4. Generate Excel template
        var fileBytes = _excelExportService.GenerateFundingRequestTemplate(categoryInfos, itemInfos, targetGroups);

        return new GenerateFundingRequestTemplateResult
        {
            FileContent = fileBytes,
            FileName = $"Mau_Yeu_Cau_Cap_Tien_{DateTime.UtcNow.AddHours(7):yyyyMMdd}.xlsx"
        };
    }
}
