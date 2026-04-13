using FluentValidation;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ResolveDepotClosure;

public class ResolveDepotClosureCommandValidator : AbstractValidator<ResolveDepotClosureCommand>
{
    public ResolveDepotClosureCommandValidator()
    {
        RuleFor(x => x.DepotId).GreaterThan(0).WithMessage("Id kho phải lớn hơn 0.");
        RuleFor(x => x.ClosureId).GreaterThan(0).WithMessage("Id bản ghi đóng kho không hợp lệ.");
        RuleFor(x => x.PerformedBy).NotEmpty().WithMessage("Thông tin người thực hiện không hợp lệ.");
        RuleFor(x => x.ResolutionType).IsInEnum().WithMessage("Hình thức giải quyết không hợp lệ.");

        // Option 1: Transfer
        When(x => x.ResolutionType == CloseResolutionType.TransferToDepot, () =>
        {
            RuleFor(x => x.TargetDepotId)
                .NotNull().WithMessage("Phải chọn kho đích khi chuyển hàng.")
                .GreaterThan(0).WithMessage("Id kho đích không hợp lệ.")
                .Must((cmd, targetId) => targetId != cmd.DepotId)
                .WithMessage("Kho đích không được trùng với kho nguồn.");
        });

        // Option 2: External - chỉ cần ghi chú mô tả cách xử lý
        When(x => x.ResolutionType == CloseResolutionType.ExternalResolution, () =>
        {
            RuleFor(x => x.ExternalNote)
                .NotEmpty().WithMessage("Phải ghi chú mô tả cách xử lý hàng tồn bên ngoài.")
                .MaximumLength(1000).WithMessage("Ghi chú tối đa 1000 ký tự.");
        });
    }
}
