namespace RESQ.Application.UseCases.Logistics.Commands.ChangeDepotStatus;

public enum ChangeableDepotStatus
{
    Available,
    Unavailable,
    Closing
}

public class ChangeDepotStatusRequestDto
{
    public ChangeableDepotStatus Status { get; set; }
}
