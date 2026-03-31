using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchasedInventoryCommand : IRequest<ImportPurchasedInventoryResponse>
{
    public Guid UserId { get; set; }
    public string? AdvancedByName { get; set; }
    public List<ImportPurchaseGroupDto> Invoices { get; set; } = new();
}
