namespace RESQ.Application.UseCases.Emergency.Commands.UpdateSosRequestVictim;

public class UpdateSosRequestVictimResponse
{
    public int SosRequestId { get; set; }
    public string UpdateType { get; set; } = "VictimUpdate";
    public DateTime UpdatedAt { get; set; }
}