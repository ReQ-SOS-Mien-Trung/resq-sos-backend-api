using MediatR;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetFundingRequests;

public class GetFundingRequestsHandler : IRequestHandler<GetFundingRequestsQuery, PagedResult<FundingRequestListDto>>
{
    private readonly IFundingRequestRepository _repository;

    public GetFundingRequestsHandler(IFundingRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<FundingRequestListDto>> Handle(GetFundingRequestsQuery request, CancellationToken cancellationToken)
    {
        var statusStrings = request.Statuses?.Select(s => s.ToString()).ToList();

        var pagedModels = await _repository.GetPagedAsync(
            request.PageNumber, request.PageSize,
            request.DepotIds, statusStrings,
            cancellationToken);

        var dtos = pagedModels.Items.Select(x => new FundingRequestListDto
        {
            Id = x.Id,
            DepotId = x.DepotId,
            DepotName = x.DepotName ?? string.Empty,
            TotalAmount = x.TotalAmount,
            Description = x.Description,
            AttachmentUrl = x.AttachmentUrl,
            Status = x.Status.ToString(),
            ApprovedCampaignId = x.ApprovedCampaignId,
            ApprovedCampaignName = x.ApprovedCampaignName,
            RequestedByUserName = x.RequestedByUserName,
            ReviewedByUserName = x.ReviewedByUserName,
            RejectionReason = x.RejectionReason,
            CreatedAt = x.CreatedAt,
            ReviewedAt = x.ReviewedAt,
            Items = x.Items.Select(i => new FundingRequestItemListDto
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
                ReceivedDate = i.ReceivedDate,
                ExpiredDate  = i.ExpiredDate,
                Notes        = i.Notes
            }).ToList()
        }).ToList();

        return new PagedResult<FundingRequestListDto>(dtos, pagedModels.TotalCount, pagedModels.PageNumber, pagedModels.PageSize);
    }
}
