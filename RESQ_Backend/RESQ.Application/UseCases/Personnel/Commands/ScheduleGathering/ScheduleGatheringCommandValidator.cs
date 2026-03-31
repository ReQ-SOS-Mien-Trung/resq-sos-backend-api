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
    }

    private static bool BeOnOrAfterTodayInVietnam(DateTime assemblyDate)
    {
        var assemblyDateInVietnam = assemblyDate.ToUtcForStorage().ToVietnamTime().Date;
        var todayInVietnam = DateTime.UtcNow.ToVietnamTime().Date;

        return assemblyDateInVietnam >= todayInVietnam;
    }
}
