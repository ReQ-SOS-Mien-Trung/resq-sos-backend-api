using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;

public class InitiateDepotClosureCommandValidator : AbstractValidator<InitiateDepotClosureCommand>
{
    public InitiateDepotClosureCommandValidator()
    {
        RuleFor(x => x.DepotId)
            .GreaterThan(0).WithMessage("Id kho phải lớn hơn 0.");

        RuleFor(x => x.InitiatedBy)
            .NotEmpty().WithMessage("Thông tin người thực hiện không hợp lệ.");

        RuleFor(x => x.Reason)
            .MaximumLength(500).When(x => x.Reason != null)
            .WithMessage("Lý do đóng kho không được vượt quá 500 ký tự.");
    }
}
