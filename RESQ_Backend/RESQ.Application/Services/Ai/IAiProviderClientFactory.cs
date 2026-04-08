using RESQ.Domain.Enum.System;

namespace RESQ.Application.Services.Ai;

public interface IAiProviderClientFactory
{
    IAiProviderClient GetClient(AiProvider provider);
}