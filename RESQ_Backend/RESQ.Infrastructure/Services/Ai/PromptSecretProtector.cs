using Microsoft.Extensions.Options;
using RESQ.Application.Services.Ai;
using RESQ.Infrastructure.Options;

namespace RESQ.Infrastructure.Services.Ai;

public class PromptSecretProtector(IOptions<PromptSecretsOptions> options)
    : AiSecretProtector(ToAiOptions(options)), IPromptSecretProtector
{
    private static IOptions<AiSecretsOptions> ToAiOptions(IOptions<PromptSecretsOptions> options)
    {
        return Microsoft.Extensions.Options.Options.Create(new AiSecretsOptions
        {
            MasterKey = options.Value.MasterKey
        });
    }
}
