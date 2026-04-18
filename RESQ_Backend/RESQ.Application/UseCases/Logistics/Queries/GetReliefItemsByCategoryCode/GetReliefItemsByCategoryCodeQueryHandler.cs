using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetReliefItemsByCategoryCode;

public class GetReliefItemsByCategoryCodeQueryHandler(IItemModelMetadataRepository itemModelMetadataRepository)
    : IRequestHandler<GetReliefItemsByCategoryCodeQuery, List<MetadataDto>>
{
    private readonly IItemModelMetadataRepository _itemModelMetadataRepository = itemModelMetadataRepository;

    public async Task<List<MetadataDto>> Handle(
        GetReliefItemsByCategoryCodeQuery request,
        CancellationToken cancellationToken)
    {
        return await _itemModelMetadataRepository.GetByCategoryCodeAsync(
            request.CategoryCode,
            cancellationToken);
    }
}
