using MediatR;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.ApproveFundingRequest;

/// <summary>
/// [Cách 2] Admin duyệt FundingRequest - chọn nguồn quỹ (Campaign hoặc SystemFund).
/// Nếu nguồn = Campaign → CampaignId bắt buộc.
/// Nếu nguồn = SystemFund → CampaignId = null, trừ tiền quỹ hệ thống.
/// </summary>
public record ApproveFundingRequestCommand(
    int FundingRequestId,
    FundSourceType SourceType,
    int? CampaignId,
    Guid ReviewedBy
) : IRequest<int>;
