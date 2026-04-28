using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RESQ.Presentation.Hubs;

[AllowAnonymous]
public class PublicDonationHub : Hub
{
    internal const string PublicDonationsGroup = "public-donations:all";

    public Task SubscribePublicDonations() =>
        Groups.AddToGroupAsync(Context.ConnectionId, PublicDonationsGroup);

    public Task UnsubscribePublicDonations() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, PublicDonationsGroup);

    public Task SubscribeCampaignDonations(int campaignId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, CampaignDonationsGroup(campaignId));

    public Task UnsubscribeCampaignDonations(int campaignId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, CampaignDonationsGroup(campaignId));

    internal static string CampaignDonationsGroup(int campaignId) => $"public-donations:campaign:{campaignId}";
}
