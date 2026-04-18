using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.CreateRepaymentTransaction;

public record RepaymentFundAllocation(
    int DepotFundId,
    decimal Amount
);

public record CreateRepaymentTransactionCommand(
    string ContributorName,
    string PhoneNumber,
    IReadOnlyCollection<RepaymentFundAllocation> Repayments,
    Guid RequestedBy
) : IRequest<Unit>;
