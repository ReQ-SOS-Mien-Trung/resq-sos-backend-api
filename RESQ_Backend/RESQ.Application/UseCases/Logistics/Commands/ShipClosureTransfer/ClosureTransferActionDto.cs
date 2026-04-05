namespace RESQ.Application.UseCases.Logistics.Commands.ShipClosureTransfer;

/// <summary>Body cho ship/receive closure transfer — chỉ cần ghi chú tuỳ chọn.</summary>
public class ClosureTransferActionDto
{
    public string? Note { get; set; }
}
