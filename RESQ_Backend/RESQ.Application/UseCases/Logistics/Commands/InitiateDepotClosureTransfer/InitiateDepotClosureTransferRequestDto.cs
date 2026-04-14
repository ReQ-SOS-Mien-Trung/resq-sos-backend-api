namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosureTransfer;

/// <summary>Request body cho POST /{id}/close/transfer.</summary>
public class InitiateDepotClosureTransferRequestDto
{
    /// <summary>Lý do dóng kho (tùy ch?n).</summary>
    public string? Reason { get; set; }

    /// <summary>Danh sách phân b? v?t ph?m sang các kho dích.</summary>
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
