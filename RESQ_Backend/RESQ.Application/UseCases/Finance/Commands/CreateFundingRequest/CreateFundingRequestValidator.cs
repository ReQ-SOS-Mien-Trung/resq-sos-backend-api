using FluentValidation;

namespace RESQ.Application.UseCases.Finance.Commands.CreateFundingRequest;

public class CreateFundingRequestValidator : AbstractValidator<CreateFundingRequestCommand>
{
    public CreateFundingRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Danh sách vật tư không được để trống.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ItemName)
                .NotEmpty().WithMessage("Tên vật tư không được để trống.");
            item.RuleFor(i => i.CategoryCode)
                .NotEmpty().WithMessage("Mã danh mục không được để trống.");
            item.RuleFor(i => i.ItemType)
                .NotEmpty().WithMessage("Loại vật phẩm không được để trống.");
            item.RuleFor(i => i.TargetGroup)
                .NotEmpty().WithMessage("Nhóm đối tượng không được để trống.");
            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0.");
            item.RuleFor(i => i.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Đơn giá không được âm.");
            item.RuleFor(i => i.TotalPrice)
                .GreaterThan(0).WithMessage("Thành tiền phải lớn hơn 0.");
        });

        RuleFor(x => x.RequestedBy)
            .NotEmpty().WithMessage("Người yêu cầu không hợp lệ.");
    }
}
