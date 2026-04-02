using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Logistics.Queries.GetSupplyRequests;

public class GetSupplyRequestsResponse
{
    public PagedResult<SupplyRequestDto> Data { get; set; } = null!;

    /// <summary>Thời điểm server xử lý request (UTC+7). Dùng để đồng bộ countdown phía FE.</summary>
    public DateTimeOffset ServerTime { get; set; }
}
