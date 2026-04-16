using MediatR;
using RESQ.Application.UseCases.SystemConfig.Commands.PromptVersioning;

namespace RESQ.Application.UseCases.SystemConfig.Commands.RollbackPromptVersion;

public record RollbackPromptVersionCommand(int Id) : IRequest<PromptVersionActionResponse>;
