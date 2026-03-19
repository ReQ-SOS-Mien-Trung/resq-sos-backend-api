using FluentValidation;

namespace RESQ.Application.UseCases.Personnel.Commands.AssignTeamsToAssemblyPoint;

public class AssignTeamsToAssemblyPointCommandValidator : AbstractValidator<AssignTeamsToAssemblyPointCommand>
{
    public AssignTeamsToAssemblyPointCommandValidator()
    {
        RuleFor(x => x.AssemblyPointId)
            .GreaterThan(0).WithMessage("AssemblyPointId không hợp lệ.");

        RuleFor(x => x.TeamIds)
            .NotEmpty().WithMessage("Danh sách đội không được rỗng.")
            .Must(ids => ids.All(id => id > 0)).WithMessage("Tất cả TeamId phải lớn hơn 0.")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Danh sách đội không được trùng lặp.");
    }
}
