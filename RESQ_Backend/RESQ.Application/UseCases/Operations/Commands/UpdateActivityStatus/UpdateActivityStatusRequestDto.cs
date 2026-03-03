using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;

public class UpdateActivityStatusRequestDto
{
    public MissionActivityStatus Status { get; set; }
}
