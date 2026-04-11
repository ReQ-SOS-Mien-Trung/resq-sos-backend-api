using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.CreateAdvanceTransaction;

/// <summary>
/// Domain command for creating a personal advance transaction for a depot fund.
/// </summary>
public record CreateAdvanceTransactionCommand(
    int DepotFundId,
    decimal Amount,
    string ContributorName,
    Guid? ContributorId,
    Guid CreatedBy
) : IRequest<Unit>;
