namespace RESQ.Application.UseCases.Logistics.Commands.CreateSupplyRequest;

public class CreateSupplyRequestRequest
{
    public List<SupplyRequestGroupDto> Requests { get; set; } = new();
}
