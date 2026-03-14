using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.ExportInventoryMovement;

public class ExportInventoryMovementQueryHandler(
    IInventoryMovementExportRepository exportRepository,
    IDepotInventoryRepository depotInventoryRepository,
    IDepotRepository depotRepository,
    IExcelExportService excelExportService,
    ILogger<ExportInventoryMovementQueryHandler> logger)
    : IRequestHandler<ExportInventoryMovementQuery, ExportInventoryMovementResult>
{
    private readonly IInventoryMovementExportRepository _exportRepository = exportRepository;
    private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
    private readonly IDepotRepository _depotRepository = depotRepository;
    private readonly IExcelExportService _excelExportService = excelExportService;
    private readonly ILogger<ExportInventoryMovementQueryHandler> _logger = logger;

    public async Task<ExportInventoryMovementResult> Handle(
        ExportInventoryMovementQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve kho của người dùng
        var depotId = await _depotInventoryRepository.GetActiveDepotIdByManagerAsync(request.UserId, cancellationToken);

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
            ExportPeriodType.ByMonth =>
                InventoryMovementExportPeriod.ForMonth(request.Year!.Value, request.Month!.Value),

            ExportPeriodType.ByYear =>
                InventoryMovementExportPeriod.ForYear(request.Year!.Value),

            ExportPeriodType.ByMonthRange =>
                InventoryMovementExportPeriod.ForMonthRange(
                    request.FromYear!.Value, request.FromMonth!.Value,
                    request.ToYear!.Value, request.ToMonth!.Value),

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
