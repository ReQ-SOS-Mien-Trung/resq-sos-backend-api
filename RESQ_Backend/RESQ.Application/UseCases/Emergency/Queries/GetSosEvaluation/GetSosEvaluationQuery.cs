using MediatR;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosEvaluation;

public record GetSosEvaluationQuery(
    int SosRequestId,
    Guid RequestingUserId,
    bool HasPrivilegedAccess
) : IRequest<GetSosEvaluationResponse>;
