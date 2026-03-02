using FluentValidation;

namespace RESQ.Application.UseCases.Emergency.Commands.GenerateRescueMissionSuggestion;

public class GenerateRescueMissionSuggestionValidator : AbstractValidator<GenerateRescueMissionSuggestionCommand>
{
    public GenerateRescueMissionSuggestionValidator()
    {
        RuleFor(x => x.ClusterId)
            .GreaterThan(0).WithMessage("ClusterId không hợp lệ");

        RuleFor(x => x.RequestedByUserId)
            .NotEmpty().WithMessage("UserId không hợp lệ");
    }
}
