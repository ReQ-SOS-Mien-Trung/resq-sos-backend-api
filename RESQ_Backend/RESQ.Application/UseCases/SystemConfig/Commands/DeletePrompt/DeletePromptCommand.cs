using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Commands.DeletePrompt;

public record DeletePromptCommand(int Id) : IRequest;
