using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RESQ.Presentation.Hubs;

[Authorize]
public class AdminFinanceHub : Hub
{
    internal const string FundingRequestsGroup = "admin-finance:funding-requests";
    internal const string CampaignsGroup = "admin-finance:campaigns";
    internal const string DisbursementsGroup = "admin-finance:disbursements";

    public Task SubscribeFundingRequests() =>
        Groups.AddToGroupAsync(Context.ConnectionId, FundingRequestsGroup);

    public Task UnsubscribeFundingRequests() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, FundingRequestsGroup);

    public Task SubscribeFundingRequest(int requestId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, FundingRequestGroup(requestId));

    public Task UnsubscribeFundingRequest(int requestId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, FundingRequestGroup(requestId));

    public Task SubscribeCampaigns() =>
        Groups.AddToGroupAsync(Context.ConnectionId, CampaignsGroup);

    public Task UnsubscribeCampaigns() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, CampaignsGroup);

    public Task SubscribeCampaign(int campaignId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, CampaignGroup(campaignId));

    public Task UnsubscribeCampaign(int campaignId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, CampaignGroup(campaignId));

    public Task SubscribeCampaignFundFlow(int campaignId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, CampaignFundFlowGroup(campaignId));

    public Task UnsubscribeCampaignFundFlow(int campaignId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, CampaignFundFlowGroup(campaignId));

    public Task SubscribeDisbursements() =>
        Groups.AddToGroupAsync(Context.ConnectionId, DisbursementsGroup);

    public Task UnsubscribeDisbursements() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, DisbursementsGroup);

    internal static string FundingRequestGroup(int requestId) => $"admin-finance:funding-request:{requestId}";
    internal static string CampaignGroup(int campaignId) => $"admin-finance:campaign:{campaignId}";
    internal static string CampaignFundFlowGroup(int campaignId) => $"admin-finance:campaign-fund-flow:{campaignId}";
}
