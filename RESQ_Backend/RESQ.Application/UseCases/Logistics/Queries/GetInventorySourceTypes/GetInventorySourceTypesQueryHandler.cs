using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Logistics.Common;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetInventorySourceTypes;

public class GetInventorySourceTypesQueryHandler : IRequestHandler<GetInventorySourceTypesQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetInventorySourceTypesQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<InventorySourceType>()
            .Select(sourceType => new MetadataDto
            {
                Key = sourceType.ToString(),
                Value = InventoryLogMetadataMappings.GetSourceTypeDisplayName(sourceType)
            })
            .ToList();

        return Task.FromResult(result);
    }
}
