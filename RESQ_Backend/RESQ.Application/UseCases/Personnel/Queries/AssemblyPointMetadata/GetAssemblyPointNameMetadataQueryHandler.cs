using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.AssemblyPointMetadata;

public class GetAssemblyPointNameMetadataQueryHandler(IAssemblyPointRepository repository)
    : IRequestHandler<GetAssemblyPointNameMetadataQuery, List<MetadataDto>>
{
    public async Task<List<MetadataDto>> Handle(
        GetAssemblyPointNameMetadataQuery request,
        CancellationToken cancellationToken)
    {
        var assemblyPoints = await repository.GetAllAsync(cancellationToken);

        return assemblyPoints
            .Where(ap => !string.IsNullOrWhiteSpace(ap.Name))
            .Select(ap => ap.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .Select(name => new MetadataDto
            {
                Key = name,
                Value = name
            })
            .ToList();
    }
}
