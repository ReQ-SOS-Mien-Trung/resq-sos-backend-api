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

    /// <summary>Năm. Bắt buộc khi PeriodType = ByMonth hoặc ByYear.</summary>
    public int? Year { get; set; }

    // --- ByMonthRange ---
    /// <summary>Tháng bắt đầu (1-12). Bắt buộc khi PeriodType = ByMonthRange.</summary>
    public int? FromMonth { get; set; }

    /// <summary>Năm bắt đầu. Bắt buộc khi PeriodType = ByMonthRange.</summary>
    public int? FromYear { get; set; }

    /// <summary>Tháng kết thúc (1-12). Bắt buộc khi PeriodType = ByMonthRange.</summary>
    public int? ToMonth { get; set; }

    /// <summary>Năm kết thúc. Bắt buộc khi PeriodType = ByMonthRange.</summary>
    public int? ToYear { get; set; }
}

public class ExportInventoryMovementResult
{
    public byte[] FileContent { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
