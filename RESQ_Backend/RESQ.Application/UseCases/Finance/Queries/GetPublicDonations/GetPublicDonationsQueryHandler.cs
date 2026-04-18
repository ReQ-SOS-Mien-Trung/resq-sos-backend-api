using MediatR;
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
        // Force isPrivate = false
        var pagedResult = await _donationRepository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            request.FundCampaignId,
            isPrivate: false, 
            cancellationToken
        );

        var dtos = pagedResult.Items.Select(donation => new GetDonationsResponseDto
        {
            Id = donation.Id,
            FundCampaignId = donation.FundCampaignId,
            FundCampaignName = donation.FundCampaignName ?? string.Empty,
            DonorName = !string.IsNullOrEmpty(donation.Donor?.Name) ? donation.Donor.Name : "Nhà hảo tâm",
            DonorEmail = donation.Donor?.Email,
            Amount = donation.Amount?.Amount ?? 0,
            Note = donation.Note,
            CreatedAt = donation.CreatedAt.ToVietnamTime(),
            IsPrivate = false
        }).ToList();

        return new PagedResult<GetDonationsResponseDto>(
            dtos,
            pagedResult.TotalCount,
            pagedResult.PageNumber,
            pagedResult.PageSize
        );
    }
}
