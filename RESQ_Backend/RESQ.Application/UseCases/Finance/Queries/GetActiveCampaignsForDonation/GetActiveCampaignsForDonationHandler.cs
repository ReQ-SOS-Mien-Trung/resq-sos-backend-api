using MediatR;
using RESQ.Application.Repositories.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetActiveCampaignsForDonation;

public class GetActiveCampaignsForDonationHandler(IFundCampaignRepository repository)
    : IRequestHandler<GetActiveCampaignsForDonationQuery, List<ActiveCampaignForDonationDto>>
{
    public async Task<List<ActiveCampaignForDonationDto>> Handle(
        GetActiveCampaignsForDonationQuery request,
        CancellationToken cancellationToken)
    {
        var campaigns = await repository.GetActiveAsync(cancellationToken);

        return campaigns.Select(campaign => new ActiveCampaignForDonationDto
        {
            Id = campaign.Id,
            Code = campaign.Code,
            Name = campaign.Name,
            Region = campaign.Region,
            TargetAmount = campaign.TargetAmount ?? 0,
            TotalAmount = campaign.TotalAmount ?? 0,
            CurrentBalance = campaign.CurrentBalance ?? 0,
            CampaignStartDate = campaign.Duration?.StartDate,
            CampaignEndDate = campaign.Duration?.EndDate
        }).ToList();
    }
}
