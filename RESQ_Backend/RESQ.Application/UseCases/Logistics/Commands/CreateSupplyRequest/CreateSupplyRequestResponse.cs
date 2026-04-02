namespace RESQ.Application.UseCases.Logistics.Commands.CreateSupplyRequest;

public class CreatedSupplyRequestDto
{
    public int SupplyRequestId { get; set; }
    public int SourceDepotId { get; set; }

    /// <summary>Thời điểm yêu cầu bị tự động từ chối (UTC+7). Dùng cho countdown trên FE.</summary>
    public DateTimeOffset ResponseDeadline { get; set; }
}

public class CreateSupplyRequestResponse
{
    public List<CreatedSupplyRequestDto> CreatedRequests { get; set; } = new();
    public string Message { get; set; } = string.Empty;

    /// <summary>Thời điểm server xử lý request (UTC+7). Dùng để đồng bộ countdown.</summary>
    public DateTimeOffset ServerTime { get; set; }
}
