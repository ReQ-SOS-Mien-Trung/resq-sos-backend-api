using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.UpdateCampaignInfo;

public record UpdateCampaignInfoCommand(int CampaignId, string Name, string Region, Guid ModifiedBy) : IRequest<bool>;
