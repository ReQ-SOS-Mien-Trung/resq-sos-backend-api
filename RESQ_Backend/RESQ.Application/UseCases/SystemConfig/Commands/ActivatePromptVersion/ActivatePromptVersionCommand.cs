using MediatR;
using RESQ.Application.UseCases.SystemConfig.Commands.PromptVersioning;

namespace RESQ.Application.UseCases.SystemConfig.Commands.ActivatePromptVersion;

public record ActivatePromptVersionCommand(int Id) : IRequest<PromptVersionActionResponse>;
