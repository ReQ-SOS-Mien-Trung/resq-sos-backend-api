using System.ComponentModel.DataAnnotations;

namespace RESQ.Application.UseCases.Finance.Commands.AddDisbursementItems;

public class AddDisbursementItemsRequest
{
    [Required]
    public List<DisbursementItemRequest> Items { get; set; } = [];
}

public class DisbursementItemRequest
{
    [Required]
    public string ItemName { get; set; } = string.Empty;
    public string? Unit { get; set; }

    [Required]
    public int Quantity { get; set; }

    [Required]
    public decimal UnitPrice { get; set; }

    [Required]
    public decimal TotalPrice { get; set; }

    public string? Note { get; set; }
}
