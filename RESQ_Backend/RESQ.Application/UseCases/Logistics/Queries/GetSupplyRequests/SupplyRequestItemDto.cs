namespace RESQ.Application.UseCases.Logistics.Queries.GetSupplyRequests;

public class SupplyRequestItemDto
{
    public int     ItemModelId   { get; set; }
    public string? ItemModelName { get; set; }
    public string? Unit           { get; set; }
    public int     Quantity       { get; set; }
}
