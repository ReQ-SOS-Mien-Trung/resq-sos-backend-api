using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetInventoryActionTypes;

public class GetInventoryActionTypesQueryHandler : IRequestHandler<GetInventoryActionTypesQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetInventoryActionTypesQuery request, CancellationToken cancellationToken)
    {
        var result = new List<MetadataDto>
        {
            new() { Key = InventoryActionType.Import.ToString(), Value = "Nhập kho" },
            new() { Key = InventoryActionType.Export.ToString(), Value = "Xuất kho" },
            new() { Key = InventoryActionType.Adjust.ToString(), Value = "Điều chỉnh" },
            new() { Key = InventoryActionType.TransferIn.ToString(), Value = "Chuyển đến" },
            new() { Key = InventoryActionType.TransferOut.ToString(), Value = "Chuyển đi" },
            new() { Key = InventoryActionType.Return.ToString(), Value = "Trả lại" }
        };

        return Task.FromResult(result);
    }
}