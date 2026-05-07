using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetItemModels;

public class GetItemModelsQuery : IRequest<List<ItemModelDetailDto>>
{
    /// <summary>Lọc theo category ID (tuỳ chọn).</summary>
    public int? CategoryId { get; set; }

    /// <summary>Lọc theo loại vật phẩm: Consumable | Reusable (tuỳ chọn).</summary>
    public string? ItemType { get; set; }
}
