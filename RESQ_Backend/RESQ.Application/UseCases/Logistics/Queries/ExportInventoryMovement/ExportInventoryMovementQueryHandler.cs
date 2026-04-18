using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.ExportInventoryMovement;

public class ExportInventoryMovementQueryHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IInventoryMovementExportRepository exportRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository,
    IExcelExportService excelExportService,
    ILogger<ExportInventoryMovementQueryHandler> logger)
    : IRequestHandler<ExportInventoryMovementQuery, ExportInventoryMovementResult>
{
    private readonly IInventoryMovementExportRepository _exportRepository = exportRepository;
    private readonly RESQ.Application.Services.IManagerDepotAccessService _managerDepotAccessService = managerDepotAccessService;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IExcelExportService _excelExportService = excelExportService;
    private readonly ILogger<ExportInventoryMovementQueryHandler> _logger = logger;

    public async Task<ExportInventoryMovementResult> Handle(
        ExportInventoryMovementQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve kho của người dùng
        var depotId = await _managerDepotAccessService.ResolveAccessibleDepotIdAsync(request.UserId, request.DepotId, cancellationToken);

        // Lấy tên kho (dùng cho tiêu đề file Excel)
        var depotName = "Toàn hệ thống";
        if (depotId.HasValue)
        {
            var depot = await _depotRepository.GetByIdAsync(depotId.Value, cancellationToken);
            if (depot is not null)
                depotName = depot.Name;
        }

        // 2. Xây dựng value object khoảng thời gian
        var period = request.PeriodType switch
        {
            ExportPeriodType.ByDateRange =>
                InventoryMovementExportPeriod.ForDateRange(request.FromDate!.Value, request.ToDate!.Value),

            ExportPeriodType.ByMonth =>
                InventoryMovementExportPeriod.ForMonth(request.Year!.Value, request.Month!.Value),

            _ => throw new ArgumentOutOfRangeException(nameof(request.PeriodType), "Loại kỳ xuất không hợp lệ.")
        };

        _logger.LogInformation(
            "Exporting inventory movement report: Period={Title}, DepotId={DepotId}",
            period.DisplayTitle, depotId);

        // 3. Truy vấn dữ liệu
        var rows = await _exportRepository.GetMovementRowsAsync(period, depotId, cancellationToken);

        // 4. Gán số thứ tự
        for (int i = 0; i < rows.Count; i++)
            rows[i].RowNumber = i + 1;

        // 5. Tạo file Excel
        var fileBytes = _excelExportService.GenerateInventoryMovementReport(rows, period.DisplayTitle, depotName);

        return new ExportInventoryMovementResult
        {
            FileContent = fileBytes,
            FileName = period.GetFileName(depotName)
        };
    }
}
