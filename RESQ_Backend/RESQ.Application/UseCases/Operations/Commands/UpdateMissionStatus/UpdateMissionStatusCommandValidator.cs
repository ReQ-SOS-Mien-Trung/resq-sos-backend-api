using FluentValidation;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMissionStatus;

public class UpdateMissionStatusCommandValidator : AbstractValidator<UpdateMissionStatusCommand>
{
    public UpdateMissionStatusCommandValidator()
    {
        RuleFor(x => x.MissionId)
            .GreaterThan(0).WithMessage("MissionId phải lớn hơn 0");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Status phải là một trong: Pending, InProgress, Completed, Cancelled");
    }
}
