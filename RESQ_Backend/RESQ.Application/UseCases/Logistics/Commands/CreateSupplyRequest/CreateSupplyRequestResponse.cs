namespace RESQ.Application.UseCases.Logistics.Commands.CreateSupplyRequest;

public class CreatedSupplyRequestDto
{
    public int SupplyRequestId { get; set; }
    public int SourceDepotId { get; set; }
}

public class CreateSupplyRequestResponse
{
    public List<CreatedSupplyRequestDto> CreatedRequests { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}
