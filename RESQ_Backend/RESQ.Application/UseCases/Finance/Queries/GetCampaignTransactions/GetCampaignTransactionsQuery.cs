using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Queries.GetCampaignTransactions;

/// <summary>
/// [Admin] Lấy danh sách giao dịch tài chính của một chiến dịch gây quỹ (bắt buộc truyền campaignId).
/// </summary>
public record GetCampaignTransactionsQuery(
    int CampaignId,
    int PageNumber,
    int PageSize,
    List<TransactionType>?          Types          = null,
    List<TransactionDirection>?     Directions     = null,
    List<TransactionReferenceType>? ReferenceTypes = null
) : IRequest<PagedResult<FundTransactionDto>>;
