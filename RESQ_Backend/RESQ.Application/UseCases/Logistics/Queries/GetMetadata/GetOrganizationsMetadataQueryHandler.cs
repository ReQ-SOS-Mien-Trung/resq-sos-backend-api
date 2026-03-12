using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public class GetOrganizationsMetadataQueryHandler(IOrganizationMetadataRepository organizationRepository) 
    : IRequestHandler<GetOrganizationsMetadataQuery, List<MetadataDto>>
{
    private readonly IOrganizationMetadataRepository _organizationRepository = organizationRepository;

    public async Task<List<MetadataDto>> Handle(GetOrganizationsMetadataQuery request, CancellationToken cancellationToken)
    {
        var organizations = await _organizationRepository.GetAllActiveAsync(cancellationToken);

        return organizations.Select(o => new MetadataDto
        {
            Key = o.Id.ToString(),
            Value = o.Name
        }).ToList();
    }
}
