using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common.Formatting;
using RESQ.Application.Common.Models;
using RESQ.Application.Extensions;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using RESQ.Presentation.Hubs;

namespace RESQ.Presentation.Services;

public sealed class PublicDonationHubService(
    IHubContext<PublicDonationHub> publicDonationHubContext,
    ILogger<PublicDonationHubService> logger) : IPublicDonationHubService
{
    private const string DonationSucceededEvent = "ReceivePublicDonation";

    private readonly IHubContext<PublicDonationHub> _publicDonationHubContext = publicDonationHubContext;
    private readonly ILogger<PublicDonationHubService> _logger = logger;

    public async Task PushDonationSucceededAsync(
        DonationModel donation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var update = new PublicDonationRealtimeUpdate
            {
                DonationId = donation.Id,
                ReceiptCode = donation.OrderId ?? string.Empty,
                FundCampaignId = donation.FundCampaignId,
                FundCampaignName = donation.FundCampaignName ?? string.Empty,
                DonorName = DonationDisplayFormatter.PrivacyAwareDonorName(donation),
                Amount = donation.Amount?.Amount ?? 0,
                Note = donation.Note,
                CreatedAt = donation.CreatedAt.ToVietnamTime(),
                PaidAt = donation.PaidAt.ToVietnamTime(),
                IsPrivate = donation.IsPrivate,
                DisplayText = DonationDisplayFormatter.PublicDonationText(donation),
                ChangedAt = DateTime.UtcNow.ToVietnamTime()
            };

            var groups = new HashSet<string>(StringComparer.Ordinal)
            {
                PublicDonationHub.PublicDonationsGroup
            };

            if (donation.FundCampaignId.HasValue)
            {
                groups.Add(PublicDonationHub.CampaignDonationsGroup(donation.FundCampaignId.Value));
            }

            foreach (var group in groups)
            {
                await _publicDonationHubContext.Clients
                    .Group(group)
                    .SendAsync(DonationSucceededEvent, update, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[PublicDonationHub] Failed to push {Event} for DonationId={DonationId}",
                DonationSucceededEvent,
                donation.Id);
        }
    }
}
