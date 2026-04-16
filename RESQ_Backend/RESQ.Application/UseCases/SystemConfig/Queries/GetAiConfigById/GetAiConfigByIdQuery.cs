using MediatR;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetAiConfigById;

public record GetAiConfigByIdQuery(int Id) : IRequest<GetAiConfigByIdResponse>;

public class GetAiConfigByIdResponse
{
    public int Id { get; set; }
    public string Status { get; set; } = "Archived";
    public string Name { get; set; } = string.Empty;
    public AiProvider Provider { get; set; }
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public int MaxTokens { get; set; }
    public string? ApiUrl { get; set; }
    public bool HasApiKey { get; set; }
    public string? ApiKeyMasked { get; set; }
    public string? Version { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
