using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.UpsertWarningBandConfig;

public class UpsertWarningBandConfigCommandValidator : AbstractValidator<UpsertWarningBandConfigCommand>
{
    public UpsertWarningBandConfigCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request không du?c null.");

        RuleFor(x => x.Request.Critical)
            .GreaterThan(0m)
                .WithMessage("Critical ph?i > 0.")
            .LessThan(x => x.Request.Medium)
                .WithMessage("Critical ph?i nh? hon Medium.");

        RuleFor(x => x.Request.Medium)
            .GreaterThan(x => x.Request.Critical)
                .WithMessage("Medium ph?i l?n hon Critical.")
            .LessThan(x => x.Request.Low)
                .WithMessage("Medium ph?i nh? hon Low.");

        RuleFor(x => x.Request.Low)
            .GreaterThan(x => x.Request.Medium)
                .WithMessage("Low ph?i l?n hon Medium.");
    }
}
