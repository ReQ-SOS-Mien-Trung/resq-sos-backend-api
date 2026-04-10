using MediatR;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.UseCases.Finance.Queries.GetFundingRequests;

namespace RESQ.Application.UseCases.Finance.Queries.GetFundingRequestItems;

public class GetFundingRequestItemsHandler
    : IRequestHandler<GetFundingRequestItemsQuery, PagedResult<FundingRequestItemListDto>>
{
    private readonly IFundingRequestRepository _repository;

    public GetFundingRequestItemsHandler(IFundingRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<FundingRequestItemListDto>> Handle(
        GetFundingRequestItemsQuery request, CancellationToken cancellationToken)
    {
        var pagedModels = await _repository.GetItemsPagedAsync(
            request.FundingRequestId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var dtos = pagedModels.Items.Select(i => new FundingRequestItemListDto
        {
            Id           = i.Id,
            Row          = i.Row,
            ItemName     = i.ItemName,
            CategoryCode = i.CategoryCode,
            Unit         = i.Unit,
            Quantity     = i.Quantity,
            UnitPrice    = i.UnitPrice,
            TotalPrice   = i.TotalPrice,
            ItemType     = i.ItemType,
            TargetGroup  = TargetGroupTranslations.JoinAsVietnamese(i.TargetGroups),
            VolumePerUnit = i.VolumePerUnit,
            WeightPerUnit = i.WeightPerUnit,
            ReceivedDate = i.ReceivedDate,
            ExpiredDate  = i.ExpiredDate,
            Notes        = i.Notes
        }).ToList();

        return new PagedResult<FundingRequestItemListDto>(dtos, pagedModels.TotalCount, pagedModels.PageNumber, pagedModels.PageSize);
    }
}
