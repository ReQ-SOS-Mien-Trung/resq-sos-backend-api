using System.Text.Json.Serialization;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.UseCases.SystemConfig.Commands.ValidateSosPriorityRuleConfig;

public class ValidateSosPriorityRuleConfigRequestDto : SosPriorityRuleConfigDocument
{
    [JsonPropertyName("sos_request_id")]
    public int? SosRequestId { get; set; }
}
