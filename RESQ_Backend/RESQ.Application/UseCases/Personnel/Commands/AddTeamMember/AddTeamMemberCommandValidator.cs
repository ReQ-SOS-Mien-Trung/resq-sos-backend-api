using FluentValidation;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.Validators;

public class AddTeamMemberCommandValidator : AbstractValidator<AddTeamMemberCommand>
{
    public AddTeamMemberCommandValidator()
    {
        RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("ID Đội không hợp lệ.");
            
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("ID Người dùng không được để trống.");
    }
}