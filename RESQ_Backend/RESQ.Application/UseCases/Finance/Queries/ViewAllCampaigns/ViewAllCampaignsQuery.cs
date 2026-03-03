using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Finance.Queries.ViewAllCampaigns;

public record ViewAllCampaignsQuery(int PageNumber, int PageSize) : IRequest<PagedResult<CampaignListDto>>;
