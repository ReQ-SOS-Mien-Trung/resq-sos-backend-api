using MediatR;
using System.Text.Json.Serialization;

namespace RESQ.Application.UseCases.Logistics.Commands.UpdateItemCategory;

public class UpdateItemCategoryCommand : IRequest<Unit>
{
    [JsonIgnore]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
