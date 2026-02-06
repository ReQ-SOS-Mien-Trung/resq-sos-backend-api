using RESQ.Application.UseCases.Emergency.Queries;

namespace RESQ.Application.UseCases.Emergency.Queries.GetMySosRequests;

public class GetMySosRequestsResponse
{
    public List<SosRequestDto> SosRequests { get; set; } = [];
}