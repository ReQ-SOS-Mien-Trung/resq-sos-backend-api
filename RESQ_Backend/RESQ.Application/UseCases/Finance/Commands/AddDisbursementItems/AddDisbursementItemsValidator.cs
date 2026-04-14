using FluentValidation;

namespace RESQ.Application.UseCases.Finance.Commands.AddDisbursementItems;

public class AddDisbursementItemsValidator : AbstractValidator<AddDisbursementItemsCommand>
{
    public AddDisbursementItemsValidator()
    {
        RuleFor(x => x.DisbursementId)
            .GreaterThan(0).WithMessage("Mã gi?i ngân không h?p l?.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Danh sách v?t ph?m không du?c d? tr?ng.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ItemName)
                .NotEmpty().WithMessage("Tên v?t ph?m không du?c d? tr?ng.");
            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("S? lu?ng ph?i l?n hon 0.");
            item.RuleFor(i => i.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Ðon giá không du?c âm.");
            item.RuleFor(i => i.TotalPrice)
                .GreaterThan(0).WithMessage("Thành ti?n ph?i l?n hon 0.");
        });

        RuleFor(x => x.CallerId)
            .NotEmpty().WithMessage("Ngu?i thêm không h?p l?.");
    }
}
