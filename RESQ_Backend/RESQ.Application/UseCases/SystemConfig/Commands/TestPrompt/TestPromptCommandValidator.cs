using FluentValidation;

namespace RESQ.Application.UseCases.SystemConfig.Commands.TestPrompt;

public class TestPromptCommandValidator : AbstractValidator<TestPromptCommand>
{
    public TestPromptCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("PromptId khong hop le.");

        RuleFor(x => x.ClusterId)
            .GreaterThan(0).WithMessage("ClusterId khong hop le.");
    }
}
