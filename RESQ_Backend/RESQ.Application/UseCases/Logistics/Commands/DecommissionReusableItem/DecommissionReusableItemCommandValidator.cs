using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.DecommissionReusableItem;

public class DecommissionReusableItemCommandValidator : AbstractValidator<DecommissionReusableItemCommand>
{
    public DecommissionReusableItemCommandValidator()
    {
        RuleFor(x => x.ReusableItemId)
            .GreaterThan(0).WithMessage("ReusableItemId không hợp lệ.");

        RuleFor(x => x.Note)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập lý do tiêu hủy vật phẩm.")
            .MaximumLength(500).WithMessage("Lý do tiêu hủy không được vượt quá 500 ký tự.");
    }
}
