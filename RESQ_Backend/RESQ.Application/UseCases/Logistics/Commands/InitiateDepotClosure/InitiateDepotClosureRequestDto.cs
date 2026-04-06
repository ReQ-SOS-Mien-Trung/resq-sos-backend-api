namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;

public class InitiateDepotClosureRequestDto
{
    /// <summary>Lý do đóng kho (optional, tối đa 500 ký tự).</summary>
    public string? Reason { get; set; }
}
