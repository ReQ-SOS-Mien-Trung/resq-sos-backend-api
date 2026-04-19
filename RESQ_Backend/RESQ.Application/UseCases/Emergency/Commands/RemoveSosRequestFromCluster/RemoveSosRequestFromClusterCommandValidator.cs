using FluentValidation;

namespace RESQ.Application.UseCases.Emergency.Commands.RemoveSosRequestFromCluster;

public class RemoveSosRequestFromClusterCommandValidator : AbstractValidator<RemoveSosRequestFromClusterCommand>
{
    public RemoveSosRequestFromClusterCommandValidator()
    {
        RuleFor(x => x.ClusterId)
            .GreaterThan(0)
            .WithMessage("ClusterId không hợp lệ.");

        RuleFor(x => x.SosRequestId)
            .GreaterThan(0)
            .WithMessage("SosRequestId không hợp lệ.");

        RuleFor(x => x.RequestedByUserId)
            .NotEmpty()
            .WithMessage("RequestedByUserId không hợp lệ.");
    }
}
