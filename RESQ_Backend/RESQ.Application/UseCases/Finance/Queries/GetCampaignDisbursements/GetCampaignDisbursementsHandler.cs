using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetCampaignDisbursements;

public class GetCampaignDisbursementsHandler : IRequestHandler<GetCampaignDisbursementsQuery, PagedResult<CampaignDisbursementListDto>>
{
    private readonly ICampaignDisbursementRepository _repository;

    public GetCampaignDisbursementsHandler(ICampaignDisbursementRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<CampaignDisbursementListDto>> Handle(GetCampaignDisbursementsQuery request, CancellationToken cancellationToken)
    {
        var pagedModels = await _repository.GetPagedAsync(
            request.PageNumber, request.PageSize,
            request.CampaignId, request.DepotId,
            cancellationToken);

        var dtos = pagedModels.Items.Select(x => new CampaignDisbursementListDto
        {
            Id = x.Id,
            FundCampaignId = x.FundCampaignId,
            FundCampaignName = x.FundCampaignName,
            DepotId = x.DepotId,
            DepotName = x.DepotName,
            Amount = x.Amount,
            Purpose = x.Purpose,
            Type = x.Type.ToString(),
            FundingRequestId = x.FundingRequestId,
            CreatedByUserName = x.CreatedByUserName,
            CreatedAt = x.CreatedAt,
            Items = x.Items.Select(i => new DisbursementItemListDto
            {
                Id = i.Id,
                ItemName = i.ItemName,
                Unit = i.Unit,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice,
                Note = i.Note
            }).ToList()
        }).ToList();

        return new PagedResult<CampaignDisbursementListDto>(dtos, pagedModels.TotalCount, pagedModels.PageNumber, pagedModels.PageSize);
    }
}
