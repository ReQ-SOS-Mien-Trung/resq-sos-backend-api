using MediatR;
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
            cancellationToken
        );

        var dtos = pagedResult.Items.Select(donation =>
        {
            string displayName;
            string? displayEmail;

            if (donation.IsPrivate)
            {
                displayName = $"Nhà hảo tâm ẩn danh {donation.Id}";
                displayEmail = null;
            }
            else
            {
                displayName = donation.Donor?.Name ?? "N/A";
                displayEmail = donation.Donor?.Email;
            }

            return new GetDonationsResponseDto
            {
                Id = donation.Id,
                FundCampaignId = donation.FundCampaignId,
                FundCampaignName = donation.FundCampaignName ?? string.Empty,
                DonorName = displayName,
                DonorEmail = displayEmail,
                Amount = donation.Amount?.Amount ?? 0,
                Note = donation.Note,
                CreatedAt = donation.CreatedAt,
                IsPrivate = donation.IsPrivate
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
