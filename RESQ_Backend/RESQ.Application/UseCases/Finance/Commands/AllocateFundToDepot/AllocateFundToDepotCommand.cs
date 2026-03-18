using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.AllocateFundToDepot;

/// <summary>
/// [Cách 1] Admin chủ động cấp tiền từ Campaign → Depot.
/// </summary>
public record AllocateFundToDepotCommand(
    int FundCampaignId,
    int DepotId,
    decimal Amount,
    string? Purpose,
    Guid AllocatedBy
) : IRequest<int>;
