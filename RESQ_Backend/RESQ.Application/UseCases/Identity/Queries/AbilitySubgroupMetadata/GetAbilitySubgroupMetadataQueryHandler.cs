using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.AbilitySubgroupMetadata;

public class GetAbilitySubgroupMetadataQueryHandler(IAbilityCategoryRepository repository)
    : IRequestHandler<GetAbilitySubgroupMetadataQuery, List<MetadataDto>>
{
    public async Task<List<MetadataDto>> Handle(
        GetAbilitySubgroupMetadataQuery request,
        CancellationToken cancellationToken)
    {
        var categories = await repository.GetAllAsync(cancellationToken);

        return categories
            .SelectMany(c => c.Subgroups)
            .Select(s => new MetadataDto
            {
                Key = s.Code,
                Value = s.Description ?? s.Code
            })
            .ToList();
    }
}
