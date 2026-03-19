using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Finance.Queries.GetCampaignTransactions;

/// <summary>
/// [Admin] Lấy danh sách giao dịch tài chính của một chiến dịch gây quỹ (bắt buộc truyền campaignId).
/// </summary>
public record GetCampaignTransactionsQuery(
    int CampaignId,
    int PageNumber,
    int PageSize
) : IRequest<PagedResult<FundTransactionDto>>;
