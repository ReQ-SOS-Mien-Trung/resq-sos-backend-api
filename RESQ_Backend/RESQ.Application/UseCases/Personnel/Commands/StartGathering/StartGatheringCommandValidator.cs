using FluentValidation;

namespace RESQ.Application.UseCases.Personnel.Commands.StartGathering;

public class StartGatheringCommandValidator : AbstractValidator<StartGatheringCommand>
{
    public StartGatheringCommandValidator()
    {
        RuleFor(x => x.AssemblyEventId)
            .GreaterThan(0).WithMessage("AssemblyEventId không hợp lệ.");
    }
}
