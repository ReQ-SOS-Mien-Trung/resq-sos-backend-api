using MediatR;

namespace RESQ.Application.UseCases.Finance.Queries.GetActiveCampaignsForDonation;

public record GetActiveCampaignsForDonationQuery : IRequest<List<ActiveCampaignForDonationDto>>;
