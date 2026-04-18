using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Finance.Queries.GetDonations;

namespace RESQ.Application.UseCases.Finance.Queries.GetPublicDonations;

public record GetPublicDonationsQuery : IRequest<PagedResult<GetDonationsResponseDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int? FundCampaignId { get; init; }
}
