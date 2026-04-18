using FluentValidation;
using RESQ.Application.Extensions;

namespace RESQ.Application.UseCases.Personnel.Commands.ScheduleGathering;

public class ScheduleGatheringCommandValidator : AbstractValidator<ScheduleGatheringCommand>
{
    public ScheduleGatheringCommandValidator()
    {
        RuleFor(x => x.AssemblyPointId)
            .GreaterThan(0)
            .WithMessage("AssemblyPointId không hợp lệ.");

        RuleFor(x => x.AssemblyDate)
            .Must(BeOnOrAfterTodayInVietnam)
            .WithMessage("Ngày triệu tập không được là ngày quá khứ.");

        RuleFor(x => x.CheckInDeadline)
            .Must(d => d.ToUtcForStorage() > DateTime.UtcNow)
            .WithMessage("Thời hạn check-in phải là thời điểm trong tương lai.")
            .Must((cmd, deadline) => deadline.ToUtcForStorage() >= cmd.AssemblyDate.ToUtcForStorage())
            .WithMessage("Thời hạn check-in phải sau hoặc bằng thời gian triệu tập (assemblyDate) — có thể đặt buffer thêm giờ để rescuer vẫn có thể check-in muộn.");
    }

    private static bool BeOnOrAfterTodayInVietnam(DateTime assemblyDate)
    {
        var assemblyDateInVietnam = assemblyDate.ToUtcForStorage().ToVietnamTime().Date;
        var todayInVietnam = DateTime.UtcNow.ToVietnamTime().Date;

        return assemblyDateInVietnam >= todayInVietnam;
    }
}
