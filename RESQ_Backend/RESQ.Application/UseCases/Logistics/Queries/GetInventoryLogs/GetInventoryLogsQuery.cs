using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetInventoryLogs;

public class GetInventoryLogsQuery : IRequest<PagedResult<InventoryLogDto>>
{
    public Guid UserId { get; set; }
    public bool IsManager { get; set; }
    public int? DepotId { get; set; }
    public int? ItemModelId { get; set; }
    public List<InventoryActionType>? ActionTypes { get; set; }
    public List<InventorySourceType>? SourceTypes { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    /// <summary>Tìm kiếm tự do: ghi chú, tên người thực hiện, tên nhà cung cấp, số hóa đơn.</summary>
    public string? Search { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
