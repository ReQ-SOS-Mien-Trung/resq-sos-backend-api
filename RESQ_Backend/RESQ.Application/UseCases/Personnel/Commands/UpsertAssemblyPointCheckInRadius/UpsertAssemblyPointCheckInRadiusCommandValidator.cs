using FluentValidation;

namespace RESQ.Application.UseCases.Personnel.Commands.UpsertAssemblyPointCheckInRadius;

public class UpsertAssemblyPointCheckInRadiusCommandValidator : AbstractValidator<UpsertAssemblyPointCheckInRadiusCommand>
{
    public UpsertAssemblyPointCheckInRadiusCommandValidator()
    {
        RuleFor(x => x.AssemblyPointId)
            .GreaterThan(0).WithMessage("AssemblyPointId phải lớn hơn 0.");

        RuleFor(x => x.MaxRadiusMeters)
            .GreaterThan(0).WithMessage("Bán kính check-in phải lớn hơn 0 mét.")
            .LessThanOrEqualTo(5000).WithMessage("Bán kính check-in không được vượt quá 5000 mét (5km).");

        RuleFor(x => x.UpdatedBy)
            .NotEmpty().WithMessage("UpdatedBy không được để trống.");
    }
}
