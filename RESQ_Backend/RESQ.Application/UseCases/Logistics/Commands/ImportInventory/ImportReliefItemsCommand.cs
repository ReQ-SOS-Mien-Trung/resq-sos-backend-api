using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportInventory;

public class ImportReliefItemsCommand : IRequest<ImportReliefItemsResponse>
{
    public int DepotId { get; set; }
    public Guid UserId { get; set; }
    public int? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public string? BatchNote { get; set; }
    public List<ImportReliefItemDto> Items { get; set; } = new();
}
