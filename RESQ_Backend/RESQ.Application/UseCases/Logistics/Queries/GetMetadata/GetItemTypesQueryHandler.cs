using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public class GetItemTypesQueryHandler : IRequestHandler<GetItemTypesQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetItemTypesQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<ItemType>()
            .Select(e => new MetadataDto
            {
                Key = e.ToString(),
                Value = e switch
                {
                    ItemType.Consumable => "Tiêu thụ",
                    ItemType.Reusable => "Tái sử dụng",
                    _ => e.ToString()
                }
            }).ToList();

        return Task.FromResult(result);
    }
}
