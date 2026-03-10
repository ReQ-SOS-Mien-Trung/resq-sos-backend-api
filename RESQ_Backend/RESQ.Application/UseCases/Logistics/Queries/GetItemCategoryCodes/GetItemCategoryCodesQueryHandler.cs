using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetItemCategoryCodes;

public class GetItemCategoryCodesQueryHandler : IRequestHandler<GetItemCategoryCodesQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetItemCategoryCodesQuery request, CancellationToken cancellationToken)
    {
        var codes = Enum.GetValues<ItemCategoryCode>()
            .Select(e => new MetadataDto
            {
                Key = ((int)e).ToString(),
                Value = e.ToString()
            })
            .ToList();

        return Task.FromResult(codes);
    }
}
