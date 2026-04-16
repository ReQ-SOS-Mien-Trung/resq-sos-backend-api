namespace RESQ.Application.UseCases.SystemConfig.Commands.CreateAiConfig;

public class CreateAiConfigResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
