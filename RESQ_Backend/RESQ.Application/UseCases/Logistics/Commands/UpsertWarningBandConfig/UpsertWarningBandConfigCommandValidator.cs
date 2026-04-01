using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.UpsertWarningBandConfig;

public class UpsertWarningBandConfigCommandValidator : AbstractValidator<UpsertWarningBandConfigCommand>
{
    public UpsertWarningBandConfigCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request không được null.");

        RuleFor(x => x.Request.Critical)
            .GreaterThan(0m)
                .WithMessage("Critical phải > 0.")
            .LessThan(x => x.Request.Medium)
                .WithMessage("Critical phải nhỏ hơn Medium.");

        RuleFor(x => x.Request.Medium)
            .GreaterThan(x => x.Request.Critical)
                .WithMessage("Medium phải lớn hơn Critical.")
            .LessThan(x => x.Request.Low)
                .WithMessage("Medium phải nhỏ hơn Low.");

        RuleFor(x => x.Request.Low)
            .GreaterThan(x => x.Request.Medium)
                .WithMessage("Low phải lớn hơn Medium.");
    }
}
