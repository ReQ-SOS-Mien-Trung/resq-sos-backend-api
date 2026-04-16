using FluentValidation;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.TestPrompt;

public class TestPromptCommandValidator : AbstractValidator<TestPromptCommand>
{
    public TestPromptCommandValidator()
    {
        RuleFor(x => x.ClusterId)
            .GreaterThan(0).WithMessage("ClusterId khong hop le.");

        RuleFor(x => x.Mode)
            .IsInEnum().WithMessage("Che do test prompt khong hop le.");

        RuleFor(x => x.AiConfigId)
            .GreaterThan(0).WithMessage("AiConfigId khong hop le.")
            .When(x => x.AiConfigId.HasValue);

        When(x => x.Mode == TestPromptDraftMode.ExistingPromptDraft, () =>
        {
            RuleFor(x => x.Id)
                .NotNull().WithMessage("PromptId khong duoc de trong.")
                .GreaterThan(0).WithMessage("PromptId khong hop le.");

            RuleFor(x => x.Name)
                .MaximumLength(255).WithMessage("Ten prompt khong duoc vuot qua 255 ky tu.")
                .When(x => x.Name != null);

            RuleFor(x => x.PromptType)
                .Must(x => !x.HasValue || Enum.IsDefined(typeof(PromptType), x.Value))
                .WithMessage("Loai prompt khong hop le.");

            RuleFor(x => x.Version)
                .MaximumLength(20).WithMessage("Phien ban khong duoc vuot qua 20 ky tu.")
                .When(x => x.Version != null);
        });

        When(x => x.Mode == TestPromptDraftMode.NewPromptDraft, () =>
        {
            RuleFor(x => x.Id)
                .Null().WithMessage("PromptId phai de trong khi test prompt moi.");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Ten prompt khong duoc de trong.")
                .MaximumLength(255).WithMessage("Ten prompt khong duoc vuot qua 255 ky tu.");

            RuleFor(x => x.PromptType)
                .NotNull().WithMessage("Loai prompt khong duoc de trong.")
                .Must(x => x.HasValue && Enum.IsDefined(typeof(PromptType), x.Value))
                .WithMessage("Loai prompt khong hop le.");

            RuleFor(x => x.Purpose)
                .NotEmpty().WithMessage("Muc dich khong duoc de trong.");

            RuleFor(x => x.SystemPrompt)
                .NotEmpty().WithMessage("System prompt khong duoc de trong.");

            RuleFor(x => x.UserPromptTemplate)
                .NotEmpty().WithMessage("User prompt template khong duoc de trong.");

            RuleFor(x => x.Version)
                .NotEmpty().WithMessage("Phien ban khong duoc de trong.")
                .MaximumLength(20).WithMessage("Phien ban khong duoc vuot qua 20 ky tu.");
        });
    }
}
