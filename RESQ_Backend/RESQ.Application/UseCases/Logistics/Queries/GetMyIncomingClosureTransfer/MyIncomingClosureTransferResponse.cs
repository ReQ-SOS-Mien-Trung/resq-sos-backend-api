using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMyIncomingClosureTransfer;

/// <summary>
/// Thông tin phiên nhận hàng từ kho nguồn đang đóng cửa.
/// Chứa đủ các param (SourceDepotId, ClosureId, TransferId) để manager kho đích
/// gọi tiếp các action endpoint mà không cần biết ID từ trước.
/// </summary>
public class MyIncomingClosureTransferResponse
{
    // -- IDs cần thiết để gọi action endpoints -----------------------------
    /// <summary>depotId của kho nguồn - dùng làm route param {id} trong các endpoint đóng kho.</summary>
    public int SourceDepotId { get; set; }

    /// <summary>ID phiên đóng kho - thông tin audit, không cần dùng trong route.</summary>
    public int ClosureId { get; set; }

    /// <summary>ID bản ghi chuyển hàng - dùng làm route param {transferId}.</summary>
    public int TransferId { get; set; }

    // -- Thông tin hiển thị ------------------------------------------------
    public string SourceDepotName { get; set; } = string.Empty;

    /// <summary>
    /// Trạng thái transfer:
    /// AwaitingShipment → Preparing → Shipping → Completed (→ manager nhận hàng → Received)
    /// </summary>
    public string TransferStatus { get; set; } = string.Empty;

    public int SnapshotConsumableUnits { get; set; }
    public int SnapshotReusableUnits { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ShippedAt { get; set; }

    /// <summary>
    /// Chi tiết từng loại hàng sẽ được chuyển sang kho đích.
    /// Manager kho đích dùng để biết trước sẽ nhận gì.
    /// </summary>
    public List<ClosureInventoryItemDto> IncomingItems { get; set; } = [];
}
