using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public class GetReliefItemMetadataQueryHandler(IReliefItemMetadataRepository reliefItemMetadataRepository)
    : IRequestHandler<GetReliefItemMetadataQuery, List<MetadataDto>>
{
    private readonly IReliefItemMetadataRepository _reliefItemMetadataRepository = reliefItemMetadataRepository;

    public async Task<List<MetadataDto>> Handle(GetReliefItemMetadataQuery request, CancellationToken cancellationToken)
    {
        return await _reliefItemMetadataRepository.GetAllForMetadataAsync(cancellationToken);
    }
}
