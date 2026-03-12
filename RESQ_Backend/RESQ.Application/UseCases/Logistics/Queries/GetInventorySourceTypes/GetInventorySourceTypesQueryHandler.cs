using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetInventorySourceTypes;

public class GetInventorySourceTypesQueryHandler : IRequestHandler<GetInventorySourceTypesQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetInventorySourceTypesQuery request, CancellationToken cancellationToken)
    {
        var result = new List<MetadataDto>
        {
            new() { Key = InventorySourceType.Purchase.ToString(), Value = "Mua hàng" },
            new() { Key = InventorySourceType.Donation.ToString(), Value = "Quyên góp" },
            new() { Key = InventorySourceType.Mission.ToString(), Value = "Nhiệm vụ" },
            new() { Key = InventorySourceType.Adjustment.ToString(), Value = "Điều chỉnh" },
            new() { Key = InventorySourceType.Transfer.ToString(), Value = "Chuyển kho" },
            new() { Key = InventorySourceType.System.ToString(), Value = "Hệ thống" }
        };

        return Task.FromResult(result);
    }
}