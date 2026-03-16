using MediatR;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetSosPriorityRuleConfig;

public record GetSosPriorityRuleConfigQuery : IRequest<SosPriorityRuleConfigResponse>;

public record GetSosPriorityRuleConfigByIdQuery(int Id) : IRequest<SosPriorityRuleConfigResponse>;
