using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.ExtendCampaign;

public record ExtendCampaignCommand(int CampaignId, DateOnly NewEndDate, Guid ModifiedBy) : IRequest<bool>;