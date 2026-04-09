using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosureTransfer;

public class InitiateDepotClosureTransferCommandValidator
    : AbstractValidator<InitiateDepotClosureTransferCommand>
{
    public InitiateDepotClosureTransferCommandValidator()
    {
        RuleFor(x => x.DepotId).GreaterThan(0).WithMessage("Id kho nguồn không hợp lệ.");
        RuleFor(x => x.TargetDepotId).GreaterThan(0).WithMessage("Id kho đích không hợp lệ.");
        RuleFor(x => x.InitiatedBy).NotEmpty().WithMessage("Thông tin người thực hiện không hợp lệ.");
        RuleFor(x => x.TargetDepotId)
            .Must((cmd, targetId) => targetId != cmd.DepotId)
            .WithMessage("Kho đích không được trùng với kho nguồn.");
    }
}
