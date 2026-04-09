using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public class GetDepotStatusesQueryHandler : IRequestHandler<GetDepotStatusesQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetDepotStatusesQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<DepotStatus>()
            .Select(e => new MetadataDto
            {
                Key = e.ToString(),
                Value = e switch
                {
                    DepotStatus.Created           => "Vừa tạo, chưa có quản lý",
                    DepotStatus.PendingAssignment => "Chờ gán lại quản lý",
                    DepotStatus.Available         => "Đang hoạt động",
                    DepotStatus.Closed            => "Đã đóng cửa",
                    _                             => e.ToString()
                }
            }).ToList();

        return Task.FromResult(result);
    }
}
