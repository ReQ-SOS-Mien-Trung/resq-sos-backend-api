using MediatR;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.AllocateFundToDepot;

/// <summary>
/// [Cách 1] Admin chủ động cấp tiền từ nguồn quỹ (Campaign hoặc SystemFund) → Depot.
/// </summary>
public record AllocateFundToDepotCommand(
    /// <summary>Loại nguồn quỹ: Campaign hoặc SystemFund.</summary>
    FundSourceType SourceType,

    /// <summary>ID chiến dịch — bắt buộc khi SourceType = Campaign, null khi SystemFund.</summary>
    int? FundCampaignId,

    int DepotId,
    decimal Amount,
    string? Purpose,
    Guid AllocatedBy
) : IRequest<int>;
