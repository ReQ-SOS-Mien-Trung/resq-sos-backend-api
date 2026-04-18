namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosureTransfer;

/// <summary>Request body cho POST /{id}/close/transfer.</summary>
public class InitiateDepotClosureTransferRequestDto
{
    /// <summary>Lý do đóng kho (tùy chọn).</summary>
    public string? Reason { get; set; }

    /// <summary>Danh sách phân bổ vật phẩm sang các kho đích.</summary>
    public List<InitiateDepotClosureTransferDepotAssignmentRequestDto> Assignments { get; set; } = [];
}

public class InitiateDepotClosureTransferDepotAssignmentRequestDto
{
    public int TargetDepotId { get; set; }
    public List<InitiateDepotClosureTransferAssignmentRequestItemDto> Items { get; set; } = [];
}

public class InitiateDepotClosureTransferAssignmentRequestItemDto
{
    public int ItemModelId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
