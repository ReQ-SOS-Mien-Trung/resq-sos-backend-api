using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Finance.Queries.GetPublicCampaignSpending;

/// <summary>
/// [Công khai] Donor xem tiền campaign đã được sử dụng để mua vật tư gì.
/// Không cần đăng nhập.
/// </summary>
public record GetPublicCampaignSpendingQuery(
    int CampaignId,
    int PageNumber,
    int PageSize
) : IRequest<PublicCampaignSpendingDto>;
