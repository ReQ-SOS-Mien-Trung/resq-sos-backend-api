using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.DecommissionReusableItem;

public class DecommissionReusableItemCommandValidator : AbstractValidator<DecommissionReusableItemCommand>
{
    public DecommissionReusableItemCommandValidator()
    {
        RuleFor(x => x.ReusableItemId)
            .GreaterThan(0).WithMessage("ReusableItemId không hợp lệ.");

        RuleFor(x => x.Note)
            .MaximumLength(500).When(x => x.Note != null)
            .WithMessage("Ghi chú không được vượt quá 500 ký tự.");
    }
}
