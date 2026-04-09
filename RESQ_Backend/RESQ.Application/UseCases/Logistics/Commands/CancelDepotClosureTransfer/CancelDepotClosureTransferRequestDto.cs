namespace RESQ.Application.UseCases.Logistics.Commands.CancelDepotClosureTransfer;

public class CancelDepotClosureTransferRequestDto
{
    /// <summary>Lý do huỷ transfer (tuỳ chọn).</summary>
    public string? Reason { get; set; }
}
