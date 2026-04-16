using MediatR;
using RESQ.Application.UseCases.SystemConfig.Commands.AiConfigVersioning;

namespace RESQ.Application.UseCases.SystemConfig.Commands.ActivateAiConfigVersion;

public record ActivateAiConfigVersionCommand(int Id) : IRequest<AiConfigVersionActionResponse>;
