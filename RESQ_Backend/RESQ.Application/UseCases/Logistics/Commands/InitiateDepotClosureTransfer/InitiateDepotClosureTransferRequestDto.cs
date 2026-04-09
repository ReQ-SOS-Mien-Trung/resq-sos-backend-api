namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosureTransfer;

/// <summary>Request body cho POST /{id}/close/transfer.</summary>
public class InitiateDepotClosureTransferRequestDto
{
    /// <summary>ID kho đích sẽ tiếp nhận toàn bộ hàng tồn kho nguồn.</summary>
    public int TargetDepotId { get; set; }

    /// <summary>Lý do đóng kho (tùy chọn).</summary>
    public string? Reason { get; set; }
}
