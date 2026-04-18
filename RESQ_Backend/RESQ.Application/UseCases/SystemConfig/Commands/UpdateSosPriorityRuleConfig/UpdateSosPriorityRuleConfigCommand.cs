using MediatR;
using RESQ.Application.UseCases.SystemConfig.Queries.GetSosPriorityRuleConfig;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.UpdateSosPriorityRuleConfig;

public record UpdateSosPriorityRuleConfigCommand(
    int Id,
    SosPriorityRuleConfigDocument Config
) : IRequest<SosPriorityRuleConfigResponse>;
