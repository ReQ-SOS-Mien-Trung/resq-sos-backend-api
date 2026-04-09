using FluentValidation;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmDeliverySupplies;

public class ConfirmDeliverySuppliesCommandValidator : AbstractValidator<ConfirmDeliverySuppliesCommand>
{
    public ConfirmDeliverySuppliesCommandValidator()
    {
        RuleFor(x => x.ActivityId)
            .GreaterThan(0).WithMessage("ActivityId phải lớn hơn 0.");

        RuleFor(x => x.MissionId)
            .GreaterThan(0).WithMessage("MissionId phải lớn hơn 0.");

        RuleFor(x => x.ConfirmedBy)
            .NotEmpty().WithMessage("ConfirmedBy không được để trống.");

        RuleFor(x => x.ActualDeliveredItems)
            .NotEmpty().WithMessage("Phải cung cấp số lượng thực tế cho ít nhất một loại vật tư.");

        RuleForEach(x => x.ActualDeliveredItems).ChildRules(item =>
        {
            item.RuleFor(i => i.ItemId)
                .GreaterThan(0).WithMessage("ItemId phải lớn hơn 0.");

            item.RuleFor(i => i.ActualQuantity)
                .GreaterThanOrEqualTo(0).WithMessage("ActualQuantity phải >= 0.");
        });

        RuleFor(x => x.ActualDeliveredItems)
            .Must(items => items == null || items.Select(i => i.ItemId).Distinct().Count() == items.Count)
            .WithMessage("Danh sách vật tư không được chứa ItemId trùng lặp.");
    }
}
