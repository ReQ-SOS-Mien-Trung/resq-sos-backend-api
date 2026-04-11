using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.CreateAdvanceTransaction;

public record CreateAdvanceTransactionItem(
    decimal Amount,
    string ContributorName,
    string PhoneNumber
);

public record CreateAdvanceTransactionCommand(
    int DepotFundId,
    IReadOnlyCollection<CreateAdvanceTransactionItem> Transactions,
    Guid RequestedBy
) : IRequest<Unit>;
