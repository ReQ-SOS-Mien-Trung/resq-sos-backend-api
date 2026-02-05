using MediatR;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetItemCategoryCodes;

public class GetItemCategoryCodesQueryHandler : IRequestHandler<GetItemCategoryCodesQuery, List<ItemCategoryCodeDto>>
{
    public Task<List<ItemCategoryCodeDto>> Handle(GetItemCategoryCodesQuery request, CancellationToken cancellationToken)
    {
        var codes = Enum.GetValues<ItemCategoryCode>()
            .Select(e => new ItemCategoryCodeDto
            {
                Value = (int)e,
                Name = e.ToString()
            })
            .ToList();

        return Task.FromResult(codes);
    }
}