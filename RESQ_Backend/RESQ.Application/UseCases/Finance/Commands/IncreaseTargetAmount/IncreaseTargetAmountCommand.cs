using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.IncreaseTargetAmount;

public record IncreaseTargetAmountCommand(int CampaignId, decimal NewTarget, Guid ModifiedBy) : IRequest<bool>;
