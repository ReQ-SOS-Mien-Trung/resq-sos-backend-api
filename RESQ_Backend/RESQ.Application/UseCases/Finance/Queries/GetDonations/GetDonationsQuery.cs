using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Finance.Queries.GetDonations;

public record GetDonationsQuery : IRequest<PagedResult<GetDonationsResponseDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int? FundCampaignId { get; init; }
    public bool? IsPrivate { get; init; }
}
