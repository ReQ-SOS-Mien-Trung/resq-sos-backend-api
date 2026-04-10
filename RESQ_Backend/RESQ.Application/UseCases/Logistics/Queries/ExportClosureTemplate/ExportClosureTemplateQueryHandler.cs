using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
namespace RESQ.Application.UseCases.Logistics.Queries.ExportClosureTemplate;

public class ExportClosureTemplateQueryHandler(
    IDepotRepository depotRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IExcelExportService excelExportService,
    ILogger<ExportClosureTemplateQueryHandler> logger)
    : IRequestHandler<ExportClosureTemplateQuery, ExportClosureTemplateResponse>
{
    public async Task<ExportClosureTemplateResponse> Handle(
        ExportClosureTemplateQuery request,
        CancellationToken cancellationToken)
    {
        var depotId = await depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken)
            ?? throw new BadRequestException("Tài khoản không quản lý kho nào đang hoạt động.");

        var depot = await depotRepository.GetByIdAsync(depotId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho cứu trợ.");

        var items = await depotRepository.GetLotDetailedInventoryForClosureAsync(depotId, cancellationToken);
        if (items.Count == 0)
            throw new ConflictException("Kho không còn hàng tồn, không cần xuất mẫu xử lý.");

        var fileContent = excelExportService.GenerateClosureExternalTemplate(depot.Name, items);
        var safeDepotName = depot.Name.Replace(" ", "_");

        logger.LogInformation(
            "ExportClosureTemplate | DepotId={DepotId} UserId={UserId} Items={Count}",
            depotId,
            request.UserId,
            items.Count);

        return new ExportClosureTemplateResponse
        {
            FileContent = fileContent,
            FileName = $"Mau_Xu_Ly_Hang_Ton_Dong_Kho_{safeDepotName}_{DateTime.UtcNow.AddHours(7):yyyyMMdd}.xlsx"
        };
    }
}
