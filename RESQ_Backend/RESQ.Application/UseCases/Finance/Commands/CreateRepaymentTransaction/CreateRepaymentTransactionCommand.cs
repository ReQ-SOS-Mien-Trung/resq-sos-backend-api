using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.CreateRepaymentTransaction;

/// <summary>
/// Domain command for creating a repayment transaction for a depot fund.
/// </summary>
public record CreateRepaymentTransactionCommand(
    int DepotFundId,
    decimal Amount,
    string ContributorName,
    Guid? ContributorId,
    Guid CreatedBy
) : IRequest<Unit>;
