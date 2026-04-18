namespace RESQ.Application.UseCases.Logistics.Commands.ResolveDepotClosure;

public class ResolveDepotClosureResponse
{
    public int ClosureId { get; set; }
    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;
    public string ResolutionType { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public string Message { get; set; } = string.Empty;

    /// <summary>True khi chọn hình thức chuyển kho - transfer đang chờ 2 bên xác nhận.</summary>
    public bool TransferPending { get; set; }

    /// <summary>ID của bản ghi transfer được tạo (chỉ có khi TransferPending = true).</summary>
    public int? TransferId { get; set; }

    /// <summary>Thông tin chi tiết transfer - chỉ có khi chuyển sang kho khác.</summary>
    public TransferSummaryDto? TransferSummary { get; set; }
}

public class TransferSummaryDto
{
    public int TransferId { get; set; }
    public int TargetDepotId { get; set; }
    public string TargetDepotName { get; set; } = string.Empty;
    public string TransferStatus { get; set; } = string.Empty;
    public int SnapshotConsumableUnits { get; set; }
    public int SnapshotReusableUnits { get; set; }
    public int ReusableItemsSkipped { get; set; }
}
