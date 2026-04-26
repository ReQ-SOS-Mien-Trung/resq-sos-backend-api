using RESQ.Application.UseCases.Personnel.Commands.ScheduleGathering;

namespace RESQ.Tests.Application.UseCases.Personnel.Commands;

public class ScheduleGatheringCommandValidatorTests
{
    private readonly ScheduleGatheringCommandValidator _validator = new();

    [Fact]
    public void Validate_Passes_WhenAssemblyDateIsWithinSchedulingGracePeriod()
    {
        var command = new ScheduleGatheringCommand(
            AssemblyPointId: 1,
            AssemblyDate: DateTime.UtcNow.AddSeconds(-30),
            CheckInDeadline: DateTime.UtcNow.AddMinutes(10),
            CreatedBy: Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_Fails_WhenAssemblyDateIsOutsideSchedulingGracePeriod()
    {
        var command = new ScheduleGatheringCommand(
            AssemblyPointId: 1,
            AssemblyDate: DateTime.UtcNow.AddMinutes(-2),
            CheckInDeadline: DateTime.UtcNow.AddMinutes(10),
            CreatedBy: Guid.NewGuid());

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ScheduleGatheringCommand.AssemblyDate));
    }
}
