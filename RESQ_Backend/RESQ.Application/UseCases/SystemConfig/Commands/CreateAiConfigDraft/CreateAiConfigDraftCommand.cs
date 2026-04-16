using MediatR;
using RESQ.Application.UseCases.SystemConfig.Commands.AiConfigVersioning;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreateAiConfigDraft;

public record CreateAiConfigDraftCommand(int SourceAiConfigId) : IRequest<AiConfigVersionActionResponse>;
