using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.ApproveFundingRequest;

/// <summary>
/// [Cách 2] Admin duyệt FundingRequest — chọn campaign để rút tiền.
/// Hệ thống lấy TotalAmount trong request → giải ngân từ campaign đã chọn.
/// </summary>
public record ApproveFundingRequestCommand(
    int FundingRequestId,
    int CampaignId,
    Guid ReviewedBy
) : IRequest<int>;
