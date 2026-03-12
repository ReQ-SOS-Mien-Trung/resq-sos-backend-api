using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportInventory;

public class ImportReliefItemsCommand : IRequest<ImportReliefItemsResponse>
{
    public int OrganizationId { get; set; }
    public List<ImportReliefItemDto> Items { get; set; } = new();
}