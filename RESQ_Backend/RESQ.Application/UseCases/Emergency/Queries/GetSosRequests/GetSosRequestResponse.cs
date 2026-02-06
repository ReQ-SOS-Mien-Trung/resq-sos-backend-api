namespace RESQ.Application.UseCases.Emergency.Queries.GetSosRequests;

public class GetSosRequestResponse
{
    public SosRequestDetailDto SosRequest { get; set; } = new();
}