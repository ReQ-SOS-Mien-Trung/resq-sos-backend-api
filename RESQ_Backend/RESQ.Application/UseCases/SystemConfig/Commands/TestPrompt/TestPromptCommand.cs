using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Commands.TestPrompt;

public record TestPromptCommand(int Id) : IRequest<TestPromptResponse>;
