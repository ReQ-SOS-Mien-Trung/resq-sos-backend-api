using RESQ.Application.UseCases.Emergency.Queries;

namespace RESQ.Application.UseCases.Emergency.Queries.GetAllSosRequests;

public class GetAllSosRequestsResponse
{
    public List<SosRequestDto> SosRequests { get; set; } = [];
}