using MediatR;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdateAiConfig;

public record UpdateAiConfigCommand(
    int Id,
    string? Name,
    AiProvider? Provider,
    string? Model,
    double? Temperature,
    int? MaxTokens,
    string? ApiKey,
    string? Version,
    bool? IsActive
) : IRequest;
