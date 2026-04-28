using MediatR;
using RESQ.Application.Common.Formatting;
using RESQ.Application.Common.Models;
using RESQ.Application.Extensions;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.UseCases.Finance.Queries.GetDonations;

namespace RESQ.Application.UseCases.Finance.Queries.GetPublicDonations;

public class GetPublicDonationsQueryHandler(IDonationRepository donationRepository)
    : IRequestHandler<GetPublicDonationsQuery, PagedResult<GetDonationsResponseDto>>
{
    private readonly IDonationRepository _donationRepository = donationRepository;

    public async Task<PagedResult<GetDonationsResponseDto>> Handle(GetPublicDonationsQuery request, CancellationToken cancellationToken)
    {
        var pagedResult = await _donationRepository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            request.FundCampaignId,
            isPrivate: null,
            receiptCodeSearch: request.Search,
            cancellationToken
        );

        var dtos = pagedResult.Items.Select(donation => new GetDonationsResponseDto
        {
            Id = donation.Id,
            ReceiptCode = donation.OrderId ?? string.Empty,
            FundCampaignId = donation.FundCampaignId,
            FundCampaignName = donation.FundCampaignName ?? string.Empty,
            DonorName = DonationDisplayFormatter.PrivacyAwareDonorName(donation),
            DonorEmail = donation.IsPrivate ? null : donation.Donor?.Email,
            Amount = donation.Amount?.Amount ?? 0,
            Note = donation.Note,
            CreatedAt = donation.CreatedAt.ToVietnamTime(),
            IsPrivate = donation.IsPrivate,
            DisplayText = DonationDisplayFormatter.PublicDonationText(donation)
        }).ToList();

        return new PagedResult<GetDonationsResponseDto>(
            dtos,
            pagedResult.TotalCount,
            pagedResult.PageNumber,
            pagedResult.PageSize
        );
    }
}
