using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.CreateDonation;

public record CreateDonationCommand : IRequest<CreateDonationResponse>
{
    public int FundCampaignId { get; init; }
    public string DonorName { get; init; } = string.Empty;
    public string DonorEmail { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? Note { get; init; }
}
