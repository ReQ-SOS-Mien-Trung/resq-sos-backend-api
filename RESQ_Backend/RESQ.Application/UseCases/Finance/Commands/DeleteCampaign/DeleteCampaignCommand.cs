using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.DeleteCampaign;

public record DeleteCampaignCommand(int Id, Guid ModifiedBy) : IRequest<bool>;
