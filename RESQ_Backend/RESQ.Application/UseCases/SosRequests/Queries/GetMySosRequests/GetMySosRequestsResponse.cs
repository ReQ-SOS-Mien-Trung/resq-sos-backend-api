using RESQ.Application.UseCases.SosRequests.Queries;

namespace RESQ.Application.UseCases.SosRequests.Queries.GetMySosRequests;

public class GetMySosRequestsResponse
{
    public List<SosRequestDto> SosRequests { get; set; } = [];
}