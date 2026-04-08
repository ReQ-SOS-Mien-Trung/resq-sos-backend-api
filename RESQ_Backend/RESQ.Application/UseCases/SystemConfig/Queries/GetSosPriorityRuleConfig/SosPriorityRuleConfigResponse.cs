using System.Text.Json.Serialization;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetSosPriorityRuleConfig;

public class SosPriorityRuleConfigResponse : SosPriorityRuleConfigDocument
{
    public int Id { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
