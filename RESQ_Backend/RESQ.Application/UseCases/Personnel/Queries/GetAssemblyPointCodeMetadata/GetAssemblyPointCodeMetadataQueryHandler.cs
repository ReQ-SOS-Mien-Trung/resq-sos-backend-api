using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointCodeMetadata;

public class GetAssemblyPointCodeMetadataQueryHandler(IAssemblyPointRepository repository)
    : IRequestHandler<GetAssemblyPointCodeMetadataQuery, List<MetadataDto>>
{
    public async Task<List<MetadataDto>> Handle(
        GetAssemblyPointCodeMetadataQuery request,
        CancellationToken cancellationToken)
    {
        var assemblyPoints = await repository.GetAllAsync(cancellationToken);

        return assemblyPoints
            .Select(ap => new MetadataDto
            {
                Key = ap.Code,
                Value = ap.Name
            })
            .ToList();
    }
}
