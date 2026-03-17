namespace RESQ.Application.UseCases.Logistics.Queries.GetSupplyRequests;

public class SupplyRequestItemDto
{
    public int     ReliefItemId   { get; set; }
    public string? ReliefItemName { get; set; }
    public string? Unit           { get; set; }
    public int     Quantity       { get; set; }
}
