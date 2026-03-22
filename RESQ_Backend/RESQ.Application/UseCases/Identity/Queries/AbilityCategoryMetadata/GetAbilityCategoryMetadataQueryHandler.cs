using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.AbilityCategoryMetadata;

public class GetAbilityCategoryMetadataQueryHandler(IAbilityCategoryRepository repository)
    : IRequestHandler<GetAbilityCategoryMetadataQuery, List<MetadataDto>>
{
    public async Task<List<MetadataDto>> Handle(
        GetAbilityCategoryMetadataQuery request,
        CancellationToken cancellationToken)
    {
        var categories = await repository.GetAllAsync(cancellationToken);

        return categories
            .Select(c => new MetadataDto
            {
                Key = c.Code,
                Value = c.Description ?? c.Code
            })
            .ToList();
    }
}
