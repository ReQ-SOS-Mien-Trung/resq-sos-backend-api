using MediatR;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.ExportInventoryMovement;

public class ExportInventoryMovementQuery : IRequest<ExportInventoryMovementResult>
{
    public Guid UserId { get; set; }
    public ExportPeriodType PeriodType { get; set; }

    // --- ByMonth ---
    /// <summary>Tháng (1-12). Bắt buộc khi PeriodType = ByMonth.</summary>
    public int? Month { get; set; }

    /// <summary>Năm (VD: 2026). Bắt buộc khi PeriodType = ByMonth.</summary>
    public int? Year { get; set; }

    // --- ByDateRange ---
    /// <summary>Ngày bắt đầu (yyyy-MM-dd). Bắt buộc khi PeriodType = ByDateRange.</summary>
    public DateOnly? FromDate { get; set; }

    /// <summary>Ngày kết thúc (yyyy-MM-dd). Bắt buộc khi PeriodType = ByDateRange.</summary>
    public DateOnly? ToDate { get; set; }
}

public class ExportInventoryMovementResult
{
    public byte[] FileContent { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
