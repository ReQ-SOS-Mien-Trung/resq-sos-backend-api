namespace RESQ.Infrastructure.Options;

public sealed class AiProvidersOptions
{
    public AiProviderEndpointOptions Gemini { get; set; } = new();
    public AiProviderEndpointOptions OpenRouter { get; set; } = new();
}

public sealed class AiProviderEndpointOptions
{
    public string? ApiUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? DefaultModel { get; set; }
}