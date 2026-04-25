using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public class GetDepotItemModelsMetadataQueryHandler(
    RESQ.Application.Services.IManagerDepotAccessService managerDepotAccessService,
    IItemModelMetadataRepository itemModelMetadataRepository,
    IDepotRepository depotRepository)
    : IRequestHandler<GetDepotItemModelsMetadataQuery, List<DepotItemModelMetadataDto>>
{
    public async Task<List<DepotItemModelMetadataDto>> Handle(GetDepotItemModelsMetadataQuery request, CancellationToken cancellationToken)
    {
        var resolvedDepotId = request.DepotId;

        if (request.IsManager)
        {
            resolvedDepotId = await managerDepotAccessService.ResolveAccessibleDepotIdAsync(
                request.UserId,
                request.DepotId,
                cancellationToken)
                ?? throw new BadRequestException("Tài khoản không quản lý kho đang hoạt động được yêu cầu.");
        }
        else
        {
            var depot = await depotRepository.GetByIdAsync(request.DepotId, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy kho cứu trợ.");

            resolvedDepotId = depot.Id;
        }

        var metadata = await itemModelMetadataRepository.GetByDepotIdForMetadataAsync(resolvedDepotId, cancellationToken);
        return metadata.Select(item => new DepotItemModelMetadataDto
        {
            Key = int.Parse(item.Key),
            Value = item.Value
        }).ToList();
    }
}
