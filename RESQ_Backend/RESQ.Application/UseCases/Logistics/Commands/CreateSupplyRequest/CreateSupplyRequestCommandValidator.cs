using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.CreateSupplyRequest;

public class CreateSupplyRequestCommandValidator : AbstractValidator<CreateSupplyRequestCommand>
{
    public CreateSupplyRequestCommandValidator()
    {
        RuleFor(x => x.Requests)
            .NotEmpty().WithMessage("Danh sách yêu cầu không được để trống.");

        RuleFor(x => x.Requests)
            .Must(list => list.Select(r => r.SourceDepotId).Distinct().Count() == list.Count)
            .WithMessage("Mỗi kho nguồn chỉ được xuất hiện một lần trong yêu cầu.");

        RuleForEach(x => x.Requests).ChildRules(group =>
        {
            group.RuleFor(g => g.SourceDepotId)
                .GreaterThan(0).WithMessage("ID kho nguồn không hợp lệ.");

            group.RuleFor(g => g.Items)
                .NotEmpty().WithMessage("Mỗi kho nguồn phải có ít nhất một vật tư yêu cầu.");

            group.RuleForEach(g => g.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.ItemModelId)
                    .GreaterThan(0).WithMessage("ID vật tư không hợp lệ.");

                item.RuleFor(i => i.Quantity)
                    .GreaterThan(0).WithMessage("Số lượng yêu cầu phải lớn hơn 0.");
            });
        });
    }
}
