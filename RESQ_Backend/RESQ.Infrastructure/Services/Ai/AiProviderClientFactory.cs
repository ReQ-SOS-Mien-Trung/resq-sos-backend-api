using RESQ.Application.Services.Ai;
using RESQ.Domain.Enum.System;

namespace RESQ.Infrastructure.Services.Ai;

public class AiProviderClientFactory(IEnumerable<IAiProviderClient> clients) : IAiProviderClientFactory
{
    private readonly IReadOnlyDictionary<AiProvider, IAiProviderClient> _clients = clients
        .GroupBy(client => client.Provider)
        .ToDictionary(group => group.Key, group => group.Last());

    public IAiProviderClient GetClient(AiProvider provider)
    {
        if (_clients.TryGetValue(provider, out var client))
        {
            return client;
        }

        throw new NotSupportedException($"AI provider '{provider}' is not registered.");
    }
}
