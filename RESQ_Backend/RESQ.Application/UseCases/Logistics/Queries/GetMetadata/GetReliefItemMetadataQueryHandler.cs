using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public class GetReliefItemMetadataQueryHandler(IItemModelMetadataRepository itemModelMetadataRepository)
    : IRequestHandler<GetReliefItemMetadataQuery, List<MetadataDto>>
{
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;

    public async Task<List<MetadataDto>> Handle(GetReliefItemMetadataQuery request, CancellationToken cancellationToken)
    {
        return await _itemModelMetadataRepository.GetAllForMetadataAsync(cancellationToken);
    }
}
