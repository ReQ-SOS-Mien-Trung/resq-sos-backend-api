using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public class GetSourceDepotStatusesQueryHandler : IRequestHandler<GetSourceDepotStatusesQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetSourceDepotStatusesQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<SourceDepotStatus>()
            .Select(e => new MetadataDto
            {
                Key = e.ToString(),
                Value = e switch
                {
                    SourceDepotStatus.Pending   => "Chờ xem xét",
                    SourceDepotStatus.Accepted  => "Đã chấp nhận",
                    SourceDepotStatus.Preparing => "Đang chuẩn bị hàng",
                    SourceDepotStatus.Shipping  => "Đang vận chuyển",
                    SourceDepotStatus.Completed => "Hoàn tất",
                    SourceDepotStatus.Rejected  => "Đã từ chối",
                    _                           => e.ToString()
                }
            }).ToList();

        return Task.FromResult(result);
    }
}
