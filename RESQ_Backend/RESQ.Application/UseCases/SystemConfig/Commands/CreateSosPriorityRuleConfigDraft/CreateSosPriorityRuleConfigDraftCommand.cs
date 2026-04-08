using MediatR;
using RESQ.Application.UseCases.SystemConfig.Queries.GetSosPriorityRuleConfig;

namespace RESQ.Application.UseCases.SystemConfig.Commands.CreateSosPriorityRuleConfigDraft;

public record CreateSosPriorityRuleConfigDraftCommand(Guid? CreatedBy) : IRequest<SosPriorityRuleConfigResponse>;
