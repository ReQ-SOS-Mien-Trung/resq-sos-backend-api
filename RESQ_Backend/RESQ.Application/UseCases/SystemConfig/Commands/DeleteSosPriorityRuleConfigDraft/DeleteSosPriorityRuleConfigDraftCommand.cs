using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Commands.DeleteSosPriorityRuleConfigDraft;

public record DeleteSosPriorityRuleConfigDraftCommand(int Id) : IRequest;
