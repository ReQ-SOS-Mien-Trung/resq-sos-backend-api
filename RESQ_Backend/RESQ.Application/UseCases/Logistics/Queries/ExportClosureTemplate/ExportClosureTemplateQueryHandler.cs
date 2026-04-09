using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.ExportClosureTemplate;

public class ExportClosureTemplateQueryHandler(
    IDepotRepository depotRepository,
    IExcelExportService excelExportService,
    ILogger<ExportClosureTemplateQueryHandler> logger)
    : IRequestHandler<ExportClosureTemplateQuery, ExportClosureTemplateResponse>
{
    public async Task<ExportClosureTemplateResponse> Handle(
        ExportClosureTemplateQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Load depot
        var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kho cứu trợ.");

        // 2. Depot phải ở trạng thái Unavailable
        if (depot.Status != DepotStatus.Unavailable)
            throw new ConflictException(
                $"Kho đang ở trạng thái '{depot.Status}'. Chỉ xuất mẫu xử lý khi kho đang Unavailable.");

        // 3. Lấy chi tiết tồn kho theo từng lô
        var items = await depotRepository.GetLotDetailedInventoryForClosureAsync(request.DepotId, cancellationToken);
        if (items.Count == 0)
            throw new ConflictException("Kho không còn hàng tồn, không cần xuất mẫu xử lý.");

        // 4. Tạo file Excel (bao gồm cột đơn giá, thành tiền, chia theo lô)
        var fileContent = excelExportService.GenerateClosureExternalTemplate(depot.Name, items);
        var safeDepotName = depot.Name.Replace(" ", "_");

        logger.LogInformation("ExportClosureTemplate | DepotId={DepotId} Items={Count}", request.DepotId, items.Count);

        return new ExportClosureTemplateResponse
        {
            FileContent = fileContent,
            FileName = $"Depot_Closure_Inventory_{safeDepotName}_{DateTime.UtcNow:yyyyMMdd}.xlsx"
        };
    }
}
