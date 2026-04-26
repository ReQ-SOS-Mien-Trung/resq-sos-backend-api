using FluentValidation;
using RESQ.Application.Extensions;

namespace RESQ.Application.UseCases.Personnel.Commands.ScheduleGathering;

public class ScheduleGatheringCommandValidator : AbstractValidator<ScheduleGatheringCommand>
{
    private static readonly TimeSpan SchedulingGracePeriod = TimeSpan.FromMinutes(1);

    public ScheduleGatheringCommandValidator()
    {
        RuleFor(x => x.AssemblyPointId)
            .GreaterThan(0)
            .WithMessage("AssemblyPointId không hợp lệ.");

        RuleFor(x => x.AssemblyDate)
            .Must(BeInTheFutureOrNow)
            .WithMessage("Thời gian triệu tập không được là thời điểm trong quá khứ.");

        RuleFor(x => x.CheckInDeadline)
            .Must(d => d.ToUtcForStorage() > DateTime.UtcNow)
            .WithMessage("Thời hạn check-in phải là thời điểm trong tương lai.")
            .Must((cmd, deadline) => deadline.ToUtcForStorage() >= cmd.AssemblyDate.ToUtcForStorage())
            .WithMessage("Thời hạn check-in phải sau hoặc bằng thời gian triệu tập (assemblyDate) để rescuer vẫn có thể check-in trước khi quá deadline.");
    }

    private static bool BeInTheFutureOrNow(DateTime assemblyDate)
    {
        return assemblyDate.ToUtcForStorage() >= DateTime.UtcNow.Subtract(SchedulingGracePeriod);
    }
}
