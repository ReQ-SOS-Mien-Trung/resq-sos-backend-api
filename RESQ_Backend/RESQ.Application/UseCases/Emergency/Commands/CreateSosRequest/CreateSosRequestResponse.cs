namespace RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;

public class CreateSosRequestResponse
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public string RawMessage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime? CreatedAt { get; set; }
}