using MediatR;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.ChangeCampaignStatus;

public record ChangeCampaignStatusCommand(int CampaignId, FundCampaignStatus NewStatus, Guid ModifiedBy, string? Reason = null) : IRequest<bool>;
