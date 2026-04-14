using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosureTransfer;

public class InitiateDepotClosureTransferCommandValidator
    : AbstractValidator<InitiateDepotClosureTransferCommand>
{
    public InitiateDepotClosureTransferCommandValidator()
    {
        RuleFor(x => x.DepotId).GreaterThan(0).WithMessage("Id kho ngu?n không h?p l?.");
        RuleFor(x => x.InitiatedBy).NotEmpty().WithMessage("Thông tin ngu?i th?c hi?n không h?p l?.");
        RuleFor(x => x.Assignments)
            .NotEmpty()
            .WithMessage("C?n phân b? ít nh?t m?t kho dích.");

        RuleForEach(x => x.Assignments).ChildRules(assignment =>
        {
            assignment.RuleFor(x => x.TargetDepotId)
                .GreaterThan(0)
                .WithMessage("Id kho dích không h?p l?.");

            assignment.RuleFor(x => x.Items)
                .NotEmpty()
                .WithMessage("M?i kho dích ph?i có ít nh?t m?t v?t ph?m du?c phân b?.");

            assignment.RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(x => x.ItemModelId)
                    .GreaterThan(0)
                    .WithMessage("Id v?t ph?m không h?p l?.");

                item.RuleFor(x => x.ItemType)
                    .Must(type => type is "Consumable" or "Reusable")
                    .WithMessage("Lo?i v?t ph?m ph?i là Consumable ho?c Reusable.");

                item.RuleFor(x => x.Quantity)
                    .GreaterThan(0)
                    .WithMessage("S? lu?ng v?t ph?m ph?i l?n hon 0.");
            });
        });

        RuleFor(x => x.Assignments)
            .Must((cmd, assignments) => assignments.All(a => a.TargetDepotId != cmd.DepotId))
            .WithMessage("Kho dích không du?c trùng v?i kho ngu?n.");
    }
}

