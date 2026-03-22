using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.AssemblyPointMetadata;

public class GetAssemblyPointMetadataQueryHandler(IAssemblyPointRepository repository)
    : IRequestHandler<GetAssemblyPointMetadataQuery, List<MetadataDto>>
{
    public async Task<List<MetadataDto>> Handle(
        GetAssemblyPointMetadataQuery request,
        CancellationToken cancellationToken)
    {
        var assemblyPoints = await repository.GetAllAsync(cancellationToken);

        return assemblyPoints
            .Select(ap => new MetadataDto
            {
                Key = ap.Id.ToString(),
                Value = ap.Name
            })
            .ToList();
    }
}
