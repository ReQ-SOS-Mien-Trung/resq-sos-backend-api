using FluentValidation;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateMission;

public class UpdateMissionCommandValidator : AbstractValidator<UpdateMissionCommand>
{
    public UpdateMissionCommandValidator()
    {
        RuleFor(x => x.MissionId)
            .GreaterThan(0).WithMessage("MissionId phải lớn hơn 0");

        RuleFor(x => x.MissionType)
            .MaximumLength(50).WithMessage("MissionType không được vượt quá 50 ký tự")
            .When(x => x.MissionType != null);
    }
}
