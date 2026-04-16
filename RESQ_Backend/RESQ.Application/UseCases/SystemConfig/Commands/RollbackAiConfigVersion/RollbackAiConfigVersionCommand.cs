using MediatR;
using RESQ.Application.UseCases.SystemConfig.Commands.AiConfigVersioning;

namespace RESQ.Application.UseCases.SystemConfig.Commands.RollbackAiConfigVersion;

public record RollbackAiConfigVersionCommand(int Id) : IRequest<AiConfigVersionActionResponse>;
