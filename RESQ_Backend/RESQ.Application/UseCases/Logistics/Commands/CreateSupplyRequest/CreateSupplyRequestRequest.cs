namespace RESQ.Application.UseCases.Logistics.Commands.CreateSupplyRequest;

public class CreateSupplyRequestRequest
{
    public int DepotId { get; set; }
    public List<SupplyRequestGroupDto> Requests { get; set; } = new();
}
