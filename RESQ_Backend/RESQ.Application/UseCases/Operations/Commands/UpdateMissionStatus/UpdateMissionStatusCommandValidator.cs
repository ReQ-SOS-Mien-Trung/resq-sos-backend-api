using FluentValidation;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMissionStatus;

public class UpdateMissionStatusCommandValidator : AbstractValidator<UpdateMissionStatusCommand>
{
    private static readonly string[] AllowedStatuses = ["pending", "in_progress", "completed", "cancelled"];

    public UpdateMissionStatusCommandValidator()
    {
        RuleFor(x => x.MissionId)
            .GreaterThan(0).WithMessage("MissionId phải lớn hơn 0");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status không được để trống")
            .Must(s => AllowedStatuses.Contains(s))
            .WithMessage($"Status phải là một trong: {string.Join(", ", AllowedStatuses)}");
    }
}
