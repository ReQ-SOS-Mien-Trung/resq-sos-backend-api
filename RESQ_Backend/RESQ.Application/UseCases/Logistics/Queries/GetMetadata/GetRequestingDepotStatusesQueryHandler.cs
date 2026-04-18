using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public class GetRequestingDepotStatusesQueryHandler : IRequestHandler<GetRequestingDepotStatusesQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetRequestingDepotStatusesQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<RequestingDepotStatus>()
            .Select(e => new MetadataDto
            {
                Key = e.ToString(),
                Value = e switch
                {
                    RequestingDepotStatus.WaitingForApproval => "Chờ phê duyệt",
                    RequestingDepotStatus.Approved           => "Đã được chấp nhận",
                    RequestingDepotStatus.InTransit          => "Đang vận chuyển",
                    RequestingDepotStatus.Received           => "Đã nhận hàng",
                    RequestingDepotStatus.Rejected           => "Bị từ chối",
                    _                                        => e.ToString()
                }
            }).ToList();

        return Task.FromResult(result);
    }
}
