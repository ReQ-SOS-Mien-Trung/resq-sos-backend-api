using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosureTransfer;

public class InitiateDepotClosureTransferCommandValidator
    : AbstractValidator<InitiateDepotClosureTransferCommand>
{
    public InitiateDepotClosureTransferCommandValidator()
    {
        RuleFor(x => x.DepotId).GreaterThan(0).WithMessage("Id kho nguồn không hợp lệ.");
        RuleFor(x => x.InitiatedBy).NotEmpty().WithMessage("Thông tin người thực hiện không hợp lệ.");
        RuleFor(x => x.Assignments)
            .NotEmpty()
            .WithMessage("Cần phân bổ ít nhất một kho đích.");

        RuleForEach(x => x.Assignments).ChildRules(assignment =>
        {
            assignment.RuleFor(x => x.TargetDepotId)
                .GreaterThan(0)
                .WithMessage("Id kho đích không hợp lệ.");

            assignment.RuleFor(x => x.Items)
                .NotEmpty()
                .WithMessage("Mỗi kho đích phải có ít nhất một vật phẩm được phân bổ.");

            assignment.RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(x => x.ItemModelId)
                    .GreaterThan(0)
                    .WithMessage("Id vật phẩm không hợp lệ.");

                item.RuleFor(x => x.ItemType)
                    .Must(type => type is "Consumable" or "Reusable")
                    .WithMessage("Loại vật phẩm phải là Consumable hoặc Reusable.");

                item.RuleFor(x => x.Quantity)
                    .GreaterThan(0)
                    .WithMessage("Số lượng vật phẩm phải lớn hơn 0.");
            });
        });

        RuleFor(x => x.Assignments)
            .Must((cmd, assignments) => assignments.All(a => a.TargetDepotId != cmd.DepotId))
            .WithMessage("Kho đích không được trùng với kho nguồn.");
    }
}

