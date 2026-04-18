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
            .NotEmpty().WithMessage("Phải cung cấp số lượng thực tế cho ít nhất một loại vật phẩm.");

        RuleForEach(x => x.ActualDeliveredItems).ChildRules(item =>
        {
            item.RuleFor(i => i.ItemId)
                .GreaterThan(0).WithMessage("ItemId phải lớn hơn 0.");

            item.RuleFor(i => i.ActualQuantity)
                .GreaterThanOrEqualTo(0).WithMessage("ActualQuantity phải >= 0.");

            item.RuleForEach(i => i.LotAllocations).ChildRules(lot =>
            {
                lot.RuleFor(l => l.LotId)
                    .GreaterThan(0).WithMessage("LotId phải lớn hơn 0.");
                lot.RuleFor(l => l.QuantityTaken)
                    .GreaterThan(0).WithMessage("QuantityTaken phải lớn hơn 0.");
            });

            item.RuleFor(i => i.LotAllocations)
                .Must(lots => lots is null || lots.Select(l => l.LotId).Distinct().Count() == lots.Count)
                .WithMessage("Danh sách lot giao thực tế không được chứa LotId trùng lặp.");

            item.RuleForEach(i => i.ReusableUnits).ChildRules(unit =>
            {
                unit.RuleFor(u => u.ReusableItemId)
                    .GreaterThan(0).WithMessage("ReusableItemId phải lớn hơn 0.");
            });

            item.RuleFor(i => i.ReusableUnits)
                .Must(units => units is null || units.Select(u => u.ReusableItemId).Distinct().Count() == units.Count)
                .WithMessage("Danh sách reusable units giao thực tế không được chứa ReusableItemId trùng lặp.");
        });

        RuleFor(x => x.ActualDeliveredItems)
            .Must(items => items == null || items.Select(i => i.ItemId).Distinct().Count() == items.Count)
            .WithMessage("Danh sách vật phẩm không được chứa ItemId trùng lặp.");
    }
}
