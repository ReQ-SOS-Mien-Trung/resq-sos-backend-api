using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetReliefItemsByCategoryCode;

public class GetReliefItemsByCategoryCodeQueryHandler(IReliefItemMetadataRepository reliefItemMetadataRepository)
    : IRequestHandler<GetReliefItemsByCategoryCodeQuery, List<MetadataDto>>
{
    private readonly IReliefItemMetadataRepository _reliefItemMetadataRepository = reliefItemMetadataRepository;

    public async Task<List<MetadataDto>> Handle(
        GetReliefItemsByCategoryCodeQuery request,
        CancellationToken cancellationToken)
    {
        return await _reliefItemMetadataRepository.GetByCategoryCodeAsync(
            request.CategoryCode,
            cancellationToken);
    }
}
