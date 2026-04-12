namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;

public class InitiateDepotClosureResponse
{
    public int DepotId { get; set; }
    public string DepotName { get; set; } = string.Empty;

    /// <summary>ID bản ghi đóng kho — chỉ có khi đóng thành công (Success = true).</summary>
    public int? ClosureId { get; set; }

    /// <summary>
    /// true = kho trống, đã đóng thành công.
    /// false = kho còn hàng, admin phải xử lý tồn kho trước (chuyển kho / xử lý ngoài).
    /// </summary>
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Danh sách hàng tồn kho còn trong kho — chỉ có khi Success = false.
    /// Admin dùng để quyết định cách xử lý (chuyển kho hoặc xử lý bên ngoài).
    /// </summary>
    public List<ClosureInventoryItemDto> RemainingItems { get; set; } = [];
}

/// <summary>DTO chi tiết một dòng tồn kho trong kho cần đóng.</summary>
public class ClosureInventoryItemDto
{
    public int ItemModelId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>"Consumable" hoặc "Reusable"</summary>
    public string ItemType { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int TransferableQuantity { get; set; }
    public int BlockedQuantity { get; set; }
    public decimal? VolumePerUnit { get; set; }
    public decimal? WeightPerUnit { get; set; }
}

/// <summary>
/// DTO chi tiết tồn kho theo từng lô — dùng cho Excel template xử lý bên ngoài.
/// Consumable items được chia theo lot (ngày nhập, hạn sử dụng).
/// Reusable items được nhóm theo item model (không có lot).
/// </summary>
public class ClosureInventoryLotItemDto
{
    public int ItemModelId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>Đối tượng sử dụng (VD: "Trẻ em, Người già"). Chuỗi ghép từ TargetGroups của ItemModel.</summary>
    public string TargetGroup { get; set; } = string.Empty;

    /// <summary>"Consumable" hoặc "Reusable"</summary>
    public string ItemType { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    /// <summary>ID lô hàng (null nếu Reusable hoặc consumable không có lot).</summary>
    public int? LotId { get; set; }

    /// <summary>Ngày nhập lô hàng.</summary>
    public DateTime? ReceivedDate { get; set; }

    /// <summary>Hạn sử dụng của lô hàng.</summary>
    public DateTime? ExpiredDate { get; set; }

    /// <summary>Số lượng tồn (remaining) của lô.</summary>
    public int Quantity { get; set; }
}
