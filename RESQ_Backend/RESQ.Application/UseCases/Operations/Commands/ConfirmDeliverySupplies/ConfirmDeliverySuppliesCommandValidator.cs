using FluentValidation;

namespace RESQ.Application.UseCases.Operations.Commands.ConfirmDeliverySupplies;

public class ConfirmDeliverySuppliesCommandValidator : AbstractValidator<ConfirmDeliverySuppliesCommand>
{
    public ConfirmDeliverySuppliesCommandValidator()
    {
        RuleFor(x => x.ActivityId)
            .GreaterThan(0).WithMessage("ActivityId ph?i l?n hon 0.");

        RuleFor(x => x.MissionId)
            .GreaterThan(0).WithMessage("MissionId ph?i l?n hon 0.");

        RuleFor(x => x.ConfirmedBy)
            .NotEmpty().WithMessage("ConfirmedBy không du?c d? tr?ng.");

        RuleFor(x => x.ActualDeliveredItems)
            .NotEmpty().WithMessage("Ph?i cung c?p s? lu?ng th?c t? cho ít nh?t m?t lo?i v?t ph?m.");

        RuleForEach(x => x.ActualDeliveredItems).ChildRules(item =>
        {
            item.RuleFor(i => i.ItemId)
                .GreaterThan(0).WithMessage("ItemId ph?i l?n hon 0.");

            item.RuleFor(i => i.ActualQuantity)
                .GreaterThanOrEqualTo(0).WithMessage("ActualQuantity ph?i >= 0.");
        });

        RuleFor(x => x.ActualDeliveredItems)
            .Must(items => items == null || items.Select(i => i.ItemId).Distinct().Count() == items.Count)
            .WithMessage("Danh sách v?t ph?m không du?c ch?a ItemId trùng l?p.");
    }
}
