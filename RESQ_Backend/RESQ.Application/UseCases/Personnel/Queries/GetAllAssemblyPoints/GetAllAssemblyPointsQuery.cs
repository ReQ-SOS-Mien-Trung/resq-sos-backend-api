using MediatR;

namespace RESQ.Application.UseCases.Personnel.Queries.GetAllAssemblyPoints;

public class GetAllAssemblyPointsQuery : IRequest<GetAllAssemblyPointsResponse>
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
