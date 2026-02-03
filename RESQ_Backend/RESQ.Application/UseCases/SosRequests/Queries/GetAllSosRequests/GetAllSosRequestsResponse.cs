using RESQ.Application.UseCases.SosRequests.Queries;

namespace RESQ.Application.UseCases.SosRequests.Queries.GetAllSosRequests;

public class GetAllSosRequestsResponse
{
    public List<SosRequestDto> SosRequests { get; set; } = [];
}