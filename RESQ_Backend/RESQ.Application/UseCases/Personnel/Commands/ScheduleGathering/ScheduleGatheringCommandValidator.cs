using FluentValidation;

namespace RESQ.Application.UseCases.Personnel.Commands.ScheduleGathering;

public class ScheduleGatheringCommandValidator : AbstractValidator<ScheduleGatheringCommand>
{
    public ScheduleGatheringCommandValidator()
    {
        RuleFor(x => x.AssemblyPointId)
            .GreaterThan(0).WithMessage("AssemblyPointId không hợp lệ.");

        RuleFor(x => x.AssemblyDate)
            .GreaterThan(DateTime.UtcNow.AddHours(48))
            .WithMessage("Ngày triệu tập phải sau ít nhất 48 giờ kể từ thời điểm hiện tại.");
    }
}
