using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportPurchasedInventory;

public class ImportPurchasedInventoryCommand : IRequest<ImportPurchasedInventoryResponse>
{
    public Guid UserId { get; set; }
    public VatInvoiceDto VatInvoice { get; set; } = new();
    public List<ImportPurchasedItemDto> Items { get; set; } = new();
}
