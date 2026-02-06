namespace RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;

public class CreateSosRequestRequestDto
{
    public string RawMessage { get; set; } = null!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}