using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Finance.Queries.GetCampaignDisbursements;

public record GetCampaignDisbursementsQuery(
    int PageNumber,
    int PageSize,
    int? CampaignId = null,
    int? DepotId = null
) : IRequest<PagedResult<CampaignDisbursementListDto>>;
