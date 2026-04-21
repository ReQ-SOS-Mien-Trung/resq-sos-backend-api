using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.ClosureTransferStatusMetadata;

public class GetClosureTransferStatusMetadataQueryHandler
    : IRequestHandler<GetClosureTransferStatusMetadataQuery, List<MetadataDto>>
{
    public async Task<List<MetadataDto>> Handle(
        GetClosureTransferStatusMetadataQuery request,
        CancellationToken cancellationToken)
    {
        var result = new List<MetadataDto>
        {
            new() { Key = DepotClosureTransferStatus.AwaitingPreparation.ToString(), Value = "Chờ chuẩn bị hàng" },
            new() { Key = DepotClosureTransferStatus.Preparing.ToString(), Value = "Đang chuẩn bị hàng" },
            new() { Key = DepotClosureTransferStatus.Shipping.ToString(), Value = "Đang vận chuyển" },
            new() { Key = DepotClosureTransferStatus.Completed.ToString(), Value = "Đã giao hàng, chờ xác nhận nhận" },
            new() { Key = DepotClosureTransferStatus.Received.ToString(), Value = "Đã nhận hàng, hoàn tất" },
            new() { Key = DepotClosureTransferStatus.Cancelled.ToString(), Value = "Đã huỷ" }
        };

        return await Task.FromResult(result);
    }
}
