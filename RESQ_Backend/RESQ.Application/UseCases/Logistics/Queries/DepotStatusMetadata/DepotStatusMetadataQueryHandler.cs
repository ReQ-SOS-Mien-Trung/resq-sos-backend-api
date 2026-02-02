using MediatR;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.DepotStatusMetadata;

public class GetDepotStatusMetadataQueryHandler
    : IRequestHandler<GetDepotStatusMetadataQuery, List<DepotStatusMetadataDto>>
{
    public async Task<List<DepotStatusMetadataDto>> Handle(
        GetDepotStatusMetadataQuery request,
        CancellationToken cancellationToken)
    {
        var result = new List<DepotStatusMetadataDto>
    {
        new() { Key = DepotStatus.PendingAssignment.ToString(), Label = "Chưa có quản lý" },
        new() { Key = DepotStatus.Available.ToString(), Label = "Đang hoạt động" },
        new() { Key = DepotStatus.Full.ToString(), Label = "Đã đầy" },
        new() { Key = DepotStatus.Closed.ToString(), Label = "Đã đóng" }
    };

        return result;
    }
}
