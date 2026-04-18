using MediatR;
using RESQ.Application.UseCases.SystemConfig.Queries.GetSosPriorityRuleConfig;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.ValidateSosPriorityRuleConfig;

public record ValidateSosPriorityRuleConfigCommand(
    int? SosRequestId,
    SosPriorityRuleConfigDocument Config
) : IRequest<SosPriorityRuleConfigValidationResponse>;
