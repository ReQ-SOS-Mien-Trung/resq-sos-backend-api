using FluentValidation;
using RESQ.Application.Common.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.UpsertSupplyRequestPriorityConfig;

public class UpsertSupplyRequestPriorityConfigValidator : AbstractValidator<UpsertSupplyRequestPriorityConfigCommand>
{
    public UpsertSupplyRequestPriorityConfigValidator()
    {
        RuleFor(x => x.UrgentMinutes)
            .GreaterThan(0).WithMessage("urgentMinutes phải lớn hơn 0.");

        RuleFor(x => x.HighMinutes)
            .GreaterThan(0).WithMessage("highMinutes phải lớn hơn 0.");

        RuleFor(x => x.MediumMinutes)
            .GreaterThan(0).WithMessage("mediumMinutes phải lớn hơn 0.");

        RuleFor(x => x)
            .Must(x => SupplyRequestPriorityPolicy.IsValid(new SupplyRequestPriorityTiming(
                x.UrgentMinutes,
                x.HighMinutes,
                x.MediumMinutes)))
            .WithMessage("Thứ tự thời gian phải thoả: khẩn cấp < gấp < trung bình.");
    }
}
