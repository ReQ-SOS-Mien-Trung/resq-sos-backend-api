using MediatR;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreateAiConfig;

public record CreateAiConfigCommand(
    string Name,
    AiProvider Provider,
    string Model,
    double Temperature,
    int MaxTokens,
    string? ApiKey,
    string Version,
    bool IsActive = true
) : IRequest<CreateAiConfigResponse>;
