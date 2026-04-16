namespace RESQ.Application.UseCases.SystemConfig.Commands.AiConfigVersioning;

public class AiConfigVersionActionResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string Status { get; set; } = "Archived";
    public string Message { get; set; } = string.Empty;
}
