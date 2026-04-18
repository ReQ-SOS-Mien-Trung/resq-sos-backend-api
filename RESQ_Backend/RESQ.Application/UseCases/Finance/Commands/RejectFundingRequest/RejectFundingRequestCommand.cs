using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.RejectFundingRequest;

public record RejectFundingRequestCommand(
    int FundingRequestId,
    string Reason,
    Guid ReviewedBy
) : IRequest<Unit>;
