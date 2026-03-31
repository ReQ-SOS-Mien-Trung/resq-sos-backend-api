using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.CreateSupplyRequest;

public class SupplyRequestGroupDto
{
    public int SourceDepotId { get; set; }
    public SupplyRequestPriorityLevel PriorityLevel { get; set; } = SupplyRequestPriorityLevel.Medium;
    public List<SupplyRequestItemDto> Items { get; set; } = new();
    public string? Note { get; set; }
}
