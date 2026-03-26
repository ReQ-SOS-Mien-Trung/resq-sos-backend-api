using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.ManageMyDepotThresholds;

public class RestoreThresholdCommandValidator : AbstractValidator<RestoreThresholdCommand>
{
    public RestoreThresholdCommandValidator()
    {
        RuleFor(x => x.ConfigId)
            .GreaterThan(0).WithMessage("ConfigId không hợp lệ.");

        RuleFor(x => x.RoleId)
            .Must(r => r == 1 || r == 4).WithMessage("Chỉ admin (role=1) hoặc manager (role=4) mới được thực hiện thao tác này.");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Lý do không được vượt quá 500 ký tự.")
            .When(x => x.Reason != null);
    }
}
