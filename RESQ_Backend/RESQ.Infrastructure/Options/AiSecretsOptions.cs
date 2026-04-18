namespace RESQ.Infrastructure.Options;

public class AiSecretsOptions
{
    public string? MasterKey { get; set; }
}

public sealed class PromptSecretsOptions : AiSecretsOptions
{
}
