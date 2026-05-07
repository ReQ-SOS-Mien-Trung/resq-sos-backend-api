using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Finance.Queries.GetPublicCampaignSpending;

/// <summary>
/// [Công khai] Donor xem tiền campaign đã được sử dụng để mua vật phẩm gì.
/// Không cần đăng nhập.
/// </summary>
public record GetPublicCampaignSpendingQuery(
    int CampaignId,
    int PageNumber = 1,
    int PageSize = 10
) : IRequest<PublicCampaignSpendingDto>;
