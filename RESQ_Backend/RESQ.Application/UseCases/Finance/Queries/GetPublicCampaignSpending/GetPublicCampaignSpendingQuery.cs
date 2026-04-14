using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Finance.Queries.GetPublicCampaignSpending;

/// <summary>
/// [Công khai] Donor xem ti?n campaign dã du?c s? d?ng d? mua v?t ph?m gì.
/// Không c?n dang nh?p.
/// </summary>
public record GetPublicCampaignSpendingQuery(
    int CampaignId,
    int PageNumber,
    int PageSize
) : IRequest<PublicCampaignSpendingDto>;
