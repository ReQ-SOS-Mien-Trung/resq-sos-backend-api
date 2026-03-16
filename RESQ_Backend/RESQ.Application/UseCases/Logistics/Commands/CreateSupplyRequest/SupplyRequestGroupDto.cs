namespace RESQ.Application.UseCases.Logistics.Commands.CreateSupplyRequest;

public class SupplyRequestGroupDto
{
    public int SourceDepotId { get; set; }
    public List<SupplyRequestItemDto> Items { get; set; } = new();
    public string? Note { get; set; }
}
