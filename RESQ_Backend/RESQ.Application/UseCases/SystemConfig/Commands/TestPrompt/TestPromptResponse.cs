namespace RESQ.Application.UseCases.SystemConfig.Commands.TestPrompt;

public class TestPromptResponse
{
    public bool IsSuccess { get; set; }
    public int PromptId { get; set; }
    public string PromptName { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? AiResponse { get; set; }
    public string? ErrorMessage { get; set; }
    public int? HttpStatusCode { get; set; }
    public long ResponseTimeMs { get; set; }
}
