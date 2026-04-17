using FluentValidation;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.TestPrompt;

public class TestPromptCommandValidator : AbstractValidator<TestPromptCommand>
{
    public TestPromptCommandValidator()
    {
        RuleFor(x => x.ClusterId)
            .GreaterThan(0).WithMessage("ClusterId không hợp lệ.");

        RuleFor(x => x.Mode)
            .IsInEnum().WithMessage("Chế độ test prompt không hợp lệ.");

        RuleFor(x => x.AiConfigId)
            .GreaterThan(0).WithMessage("AiConfigId không hợp lệ.")
            .When(x => x.AiConfigId.HasValue);

        When(x => x.Mode == TestPromptDraftMode.ExistingPromptDraft, () =>
        {
            RuleFor(x => x.Id)
                .NotNull().WithMessage("PromptId không được để trống.")
                .GreaterThan(0).WithMessage("PromptId không hợp lệ.");

            RuleFor(x => x.Name)
                .MaximumLength(255).WithMessage("Tên prompt không được vượt quá 255 ký tự.")
                .When(x => x.Name != null);

            RuleFor(x => x.PromptType)
                .Must(x => !x.HasValue || Enum.IsDefined(typeof(PromptType), x.Value))
                .WithMessage("Loại prompt không hợp lệ.");

            RuleFor(x => x.Version)
                .MaximumLength(20).WithMessage("Phiên bản không được vượt quá 20 ký tự.")
                .When(x => x.Version != null);
        });

        When(x => x.Mode == TestPromptDraftMode.NewPromptDraft, () =>
        {
            RuleFor(x => x.Id)
                .Null().WithMessage("PromptId phải để trống khi test prompt mới.");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tên prompt không được để trống.")
                .MaximumLength(255).WithMessage("Tên prompt không được vượt quá 255 ký tự.");

            RuleFor(x => x.PromptType)
                .NotNull().WithMessage("Loại prompt không được để trống.")
                .Must(x => x.HasValue && Enum.IsDefined(typeof(PromptType), x.Value))
                .WithMessage("Loại prompt không hợp lệ.");

            RuleFor(x => x.Purpose)
                .NotEmpty().WithMessage("Mục đích không được để trống.");

            RuleFor(x => x.SystemPrompt)
                .NotEmpty().WithMessage("System prompt không được để trống.");

            RuleFor(x => x.UserPromptTemplate)
                .NotEmpty().WithMessage("User prompt template không được để trống.");

            RuleFor(x => x.Version)
                .NotEmpty().WithMessage("Phiên bản không được để trống.")
                .MaximumLength(20).WithMessage("Phiên bản không được vượt quá 20 ký tự.");
        });
    }
}
