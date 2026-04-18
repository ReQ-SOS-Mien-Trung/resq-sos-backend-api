using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Emergency.Exceptions;

public sealed class SosRuleEvaluationFailedException : DomainException
{
    public SosRuleEvaluationFailedException(int sosRequestId)
        : base($"Không thể đánh giá mức độ ưu tiên cho yêu cầu SOS Id: {sosRequestId}.") { }
}
