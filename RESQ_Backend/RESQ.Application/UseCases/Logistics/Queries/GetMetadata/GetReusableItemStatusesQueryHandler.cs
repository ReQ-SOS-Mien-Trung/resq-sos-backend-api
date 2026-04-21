using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public class GetReusableItemStatusesQueryHandler : IRequestHandler<GetReusableItemStatusesQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetReusableItemStatusesQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<ReusableItemStatus>()
            .Select(e => new MetadataDto
            {
                Key = e.ToString(),
                Value = e switch
                {
                    ReusableItemStatus.Available      => "Khả dụng",
                    ReusableItemStatus.Reserved       => "Đã phân bổ",
                    ReusableItemStatus.InTransit      => "Đang vận chuyển",
                    ReusableItemStatus.InUse          => "Đang sử dụng",
                    ReusableItemStatus.Maintenance    => "Đang bảo trì",
                    ReusableItemStatus.Decommissioned => "Đã loại biên",
                    _                                 => e.ToString()
                }
            }).ToList();

        return Task.FromResult(result);
    }
}
