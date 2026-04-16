using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Commands.DeleteAiConfig;

public record DeleteAiConfigCommand(int Id) : IRequest;
