using RESQ.Domain.Enum.System;

namespace RESQ.Application.Services.Ai;

public interface IAiProviderClient
{
    AiProvider Provider { get; }

    Task<AiCompletionResponse> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default);
}
