using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.ViewAllCampaigns;

public record ViewAllCampaignsQuery(
    int PageNumber,
    int PageSize,
    List<FundCampaignStatus>? Statuses = null
) : IRequest<PagedResult<CampaignListDto>>;
