namespace RESQ.Application.UseCases.Logistics.Commands.ChangeDepotStatus;

/// <summary>Chỉ cho phép chọn Available hoặc Unavailable.</summary>
public enum ChangeableDepotStatus
{
    Available,
    Unavailable
}

public class ChangeDepotStatusRequestDto
{
    public ChangeableDepotStatus Status { get; set; }
}