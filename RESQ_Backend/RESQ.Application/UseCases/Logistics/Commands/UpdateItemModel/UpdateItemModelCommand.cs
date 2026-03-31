using MediatR;
using System.Text.Json.Serialization;

namespace RESQ.Application.UseCases.Logistics.Commands.UpdateItemModel;

public class UpdateItemModelCommand : IRequest<Unit>
{
    [JsonIgnore]
    public int Id { get; set; }

    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public List<string> TargetGroups { get; set; } = new();
    public string? ImageUrl { get; set; }
}
