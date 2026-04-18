using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetRoleMetadata;

public class GetRoleMetadataQueryHandler(IRoleRepository roleRepository)
    : IRequestHandler<GetRoleMetadataQuery, List<MetadataDto>>
{
    private readonly IRoleRepository _roleRepository = roleRepository;

    public async Task<List<MetadataDto>> Handle(
        GetRoleMetadataQuery request,
        CancellationToken cancellationToken)
    {
        var roles = await _roleRepository.GetAllAsync(cancellationToken);

        return roles
            .Select(role => new MetadataDto
            {
                Key = role.Id.ToString(),
                Value = role.Name
            })
            .ToList();
    }
}
