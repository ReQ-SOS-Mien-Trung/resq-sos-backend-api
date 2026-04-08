using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.DepotStatusMetadata;

public class GetDepotStatusMetadataQueryHandler
    : IRequestHandler<GetDepotStatusMetadataQuery, List<MetadataDto>>
{
    public async Task<List<MetadataDto>> Handle(
        GetDepotStatusMetadataQuery request,
        CancellationToken cancellationToken)
    {
        var result = new List<MetadataDto>
        {
            new() { Key = DepotStatus.Created.ToString(),             Value = "Vừa tạo, chưa có quản lý" },
            new() { Key = DepotStatus.PendingAssignment.ToString(),   Value = "Chờ gán lại quản lý" },
            new() { Key = DepotStatus.Available.ToString(),           Value = "Đang hoạt động" },
            new() { Key = DepotStatus.Full.ToString(),                Value = "Đã đầy" },
            new() { Key = DepotStatus.UnderMaintenance.ToString(),    Value = "Đang bảo trì" },
            new() { Key = DepotStatus.Closing.ToString(),             Value = "Đang tiến hành đóng kho" },
            new() { Key = DepotStatus.Closed.ToString(),              Value = "Đã đóng" }
        };

        return await Task.FromResult(result);
    }
}
