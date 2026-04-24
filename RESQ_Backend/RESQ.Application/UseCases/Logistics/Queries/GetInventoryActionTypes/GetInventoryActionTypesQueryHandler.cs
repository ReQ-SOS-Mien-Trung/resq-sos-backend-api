using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Logistics.Common;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetInventoryActionTypes;

public class GetInventoryActionTypesQueryHandler : IRequestHandler<GetInventoryActionTypesQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetInventoryActionTypesQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<InventoryActionType>()
            .Select(actionType => new MetadataDto
            {
                Key = actionType.ToString(),
                Value = InventoryLogMetadataMappings.GetActionTypeDisplayName(actionType)
            })
            .ToList();

        return Task.FromResult(result);
    }
}
