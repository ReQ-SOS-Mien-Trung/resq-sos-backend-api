using MediatR;
using RESQ.Application.UseCases.SystemConfig.Queries.GetSosPriorityRuleConfig;

namespace RESQ.Application.UseCases.SystemConfig.Commands.ActivateSosPriorityRuleConfig;

public record ActivateSosPriorityRuleConfigCommand(int Id, Guid? ActivatedBy) : IRequest<SosPriorityRuleConfigResponse>;
