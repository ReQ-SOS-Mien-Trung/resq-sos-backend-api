using MediatR;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.GetAllAssemblyPointCheckInRadiusConfigs;

public class GetAllAssemblyPointCheckInRadiusConfigsQueryHandler(
    IAssemblyPointCheckInRadiusRepository radiusRepository)
    : IRequestHandler<GetAllAssemblyPointCheckInRadiusConfigsQuery, GetAllAssemblyPointCheckInRadiusConfigsResponse>
{
    public async Task<GetAllAssemblyPointCheckInRadiusConfigsResponse> Handle(
        GetAllAssemblyPointCheckInRadiusConfigsQuery request,
        CancellationToken cancellationToken)
    {
        var all = await radiusRepository.GetAllAsync(cancellationToken);

        return new GetAllAssemblyPointCheckInRadiusConfigsResponse
        {
            Items = all.Select(x => new AssemblyPointCheckInRadiusConfigItem
            {
                AssemblyPointId = x.AssemblyPointId,
                MaxRadiusMeters = x.MaxRadiusMeters,
                UpdatedBy = x.UpdatedBy,
                UpdatedAt = x.UpdatedAt
            }).ToList()
        };
    }
}
