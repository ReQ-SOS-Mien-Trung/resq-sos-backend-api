using FluentValidation;

namespace RESQ.Application.UseCases.Finance.Commands.AddDisbursementItems;

public class AddDisbursementItemsValidator : AbstractValidator<AddDisbursementItemsCommand>
{
    public AddDisbursementItemsValidator()
    {
        RuleFor(x => x.DisbursementId)
            .GreaterThan(0).WithMessage("Mã giải ngân không hợp lệ.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Danh sách vật phẩm không được để trống.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ItemName)
                .NotEmpty().WithMessage("Tên vật phẩm không được để trống.");
            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0.");
            item.RuleFor(i => i.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Đơn giá không được âm.");
            item.RuleFor(i => i.TotalPrice)
                .GreaterThan(0).WithMessage("Thành tiền phải lớn hơn 0.");
        });

        RuleFor(x => x.CallerId)
            .NotEmpty().WithMessage("Người thêm không hợp lệ.");
    }
}
