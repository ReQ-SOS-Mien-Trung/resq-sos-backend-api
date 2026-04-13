namespace RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;

public class UpdateActivityStatusRequestDto
{
    /// <summary>Target status: Planned, OnGoing, Succeed, PendingConfirmation, Failed, Cancelled</summary>
    public string Status { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}
