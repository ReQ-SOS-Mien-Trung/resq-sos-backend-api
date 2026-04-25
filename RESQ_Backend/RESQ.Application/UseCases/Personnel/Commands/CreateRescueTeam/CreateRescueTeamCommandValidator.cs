using FluentValidation;
using RESQ.Application.UseCases.Personnel.RescueTeams.Commands;

namespace RESQ.Application.UseCases.Personnel.RescueTeams.Validators;

public class CreateRescueTeamCommandValidator : AbstractValidator<CreateRescueTeamCommand>
{
    public CreateRescueTeamCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên đội không được để trống.")
            .MaximumLength(255).WithMessage("Tên đội không được vượt quá 255 ký tự.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Loại đội không hợp lệ.");

        RuleFor(x => x.AssemblyPointId)
            .GreaterThan(0).WithMessage("ID điểm tập kết không hợp lệ.");

        RuleFor(x => x.ManagedBy)
            .NotEmpty().WithMessage("ID người điều phối không được để trống.");

        RuleFor(x => x.MaxMembers)
            .InclusiveBetween(6, 8).WithMessage("Số lượng thành viên tối đa phải từ 6 đến 8.");

        RuleFor(x => x.Members)
            .NotNull().WithMessage("Danh sách thành viên không được để trống.")
            .Must((command, members) => members != null && members.Count == command.MaxMembers)
            .WithMessage(cmd => $"Số lượng thành viên truyền vào ({cmd.Members?.Count ?? 0}) phải bằng đúng số lượng tối đa của đội ({cmd.MaxMembers}).")
            .Must(members => members != null && members.All(m => m.EventId > 0))
            .WithMessage("Mỗi thành viên phải có EventId hợp lệ.")
            .Must(members => members != null && members.Select(m => m.UserId).Distinct().Count() == members.Count)
            .WithMessage("Danh sách thành viên không được chứa trùng rescuer.")
            .Must(members => members != null && members.Count(m => m.IsLeader) == 1)
            .WithMessage("Đội phải có đúng 1 đội trưởng.");
    }
}
