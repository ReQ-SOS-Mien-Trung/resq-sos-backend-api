using MediatR;
using RESQ.Application.Common.Formatting;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetDonations;

public class GetDonationsQueryHandler(IDonationRepository donationRepository)
    : IRequestHandler<GetDonationsQuery, PagedResult<GetDonationsResponseDto>>
{
    private readonly IDonationRepository _donationRepository = donationRepository;

    public async Task<PagedResult<GetDonationsResponseDto>> Handle(GetDonationsQuery request, CancellationToken cancellationToken)
    {
        var pagedResult = await _donationRepository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            request.FundCampaignId,
            request.IsPrivate,
            cancellationToken: cancellationToken
        );

        var dtos = pagedResult.Items.Select(donation =>
        {
            string displayName;
            string? displayEmail;

            if (donation.IsPrivate)
            {
                displayName = DonationDisplayFormatter.PrivacyAwareDonorName(donation);
                displayEmail = null;
            }
            else
            {
                displayName = DonationDisplayFormatter.PublicDonorName(donation);
                displayEmail = donation.Donor?.Email;
            }

            return new GetDonationsResponseDto
            {
                Id = donation.Id,
                ReceiptCode = donation.OrderId ?? string.Empty,
                FundCampaignId = donation.FundCampaignId,
                FundCampaignName = donation.FundCampaignName ?? string.Empty,
                DonorName = displayName,
                DonorEmail = displayEmail,
                Amount = donation.Amount?.Amount ?? 0,
                Note = donation.Note,
                CreatedAt = donation.CreatedAt,
                IsPrivate = donation.IsPrivate,
                DisplayText = DonationDisplayFormatter.PublicDonationText(donation)
            };
        }).ToList();

        return new PagedResult<GetDonationsResponseDto>(
            dtos,
            pagedResult.TotalCount,
            pagedResult.PageNumber,
            pagedResult.PageSize
        );
    }
}
