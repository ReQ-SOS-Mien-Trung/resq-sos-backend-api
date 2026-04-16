using MediatR;
using RESQ.Application.UseCases.SystemConfig.Commands.PromptVersioning;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreatePromptDraft;

public record CreatePromptDraftCommand(int SourcePromptId) : IRequest<PromptVersionActionResponse>;
