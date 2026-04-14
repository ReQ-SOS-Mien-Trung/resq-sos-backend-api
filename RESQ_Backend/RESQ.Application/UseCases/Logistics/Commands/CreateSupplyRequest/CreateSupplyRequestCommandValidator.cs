using FluentValidation;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.CreateSupplyRequest;

public class CreateSupplyRequestCommandValidator : AbstractValidator<CreateSupplyRequestCommand>
{
    public CreateSupplyRequestCommandValidator()
    {
        RuleFor(x => x.Requests)
            .NotEmpty().WithMessage("Danh sách yêu c?u không du?c d? tr?ng.");

        RuleFor(x => x.Requests)
            .Must(list => list.Select(r => r.SourceDepotId).Distinct().Count() == list.Count)
            .WithMessage("M?i kho ngu?n ch? du?c xu?t hi?n m?t l?n trong yêu c?u.");

        RuleForEach(x => x.Requests).ChildRules(group =>
        {
            group.RuleFor(g => g.SourceDepotId)
                .GreaterThan(0).WithMessage("ID kho ngu?n không h?p l?.");

            group.RuleFor(g => g.PriorityLevel)
                .IsInEnum()
                .Must(x => x is SupplyRequestPriorityLevel.Urgent or SupplyRequestPriorityLevel.High or SupplyRequestPriorityLevel.Medium)
                .WithMessage("M?c d? uu tiên yêu c?u ti?p t? không h?p l?.");

            group.RuleFor(g => g.Items)
                .NotEmpty().WithMessage("M?i kho ngu?n ph?i có ít nh?t m?t v?t ph?m yêu c?u.");

            group.RuleForEach(g => g.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.ItemModelId)
                    .GreaterThan(0).WithMessage("ID v?t ph?m không h?p l?.");

                item.RuleFor(i => i.Quantity)
                    .GreaterThan(0).WithMessage("S? lu?ng yêu c?u ph?i l?n hon 0.");
            });
        });
    }
}
