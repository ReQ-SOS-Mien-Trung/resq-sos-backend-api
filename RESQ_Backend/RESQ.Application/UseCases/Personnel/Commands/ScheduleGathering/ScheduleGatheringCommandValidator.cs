using FluentValidation;

namespace RESQ.Application.UseCases.Personnel.Commands.ScheduleGathering;

public class ScheduleGatheringCommandValidator : AbstractValidator<ScheduleGatheringCommand>
{
    public ScheduleGatheringCommandValidator()
    {
        RuleFor(x => x.AssemblyPointId)
            .GreaterThan(0).WithMessage("AssemblyPointId không hợp lệ.");

        RuleFor(x => x.AssemblyDate)
            .GreaterThan(DateTime.UtcNow).WithMessage("Ngày tập trung phải sau thời điểm hiện tại.");
    }
}
