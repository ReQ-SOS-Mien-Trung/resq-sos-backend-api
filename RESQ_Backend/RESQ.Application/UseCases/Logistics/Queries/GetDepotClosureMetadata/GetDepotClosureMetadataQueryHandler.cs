using MediatR;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotClosureMetadata;

public class GetDepotClosureMetadataQueryHandler
    : IRequestHandler<GetDepotClosureMetadataQuery, DepotClosureMetadataResponse>
{
    public Task<DepotClosureMetadataResponse> Handle(
        GetDepotClosureMetadataQuery request,
        CancellationToken cancellationToken)
    {
        var response = new DepotClosureMetadataResponse
        {
            ResolutionTypes =
            [
                new MetadataDto { Key = CloseResolutionType.TransferToDepot.ToString(), Value = "Chuyển toàn bộ hàng sang kho khác" },
                new MetadataDto { Key = CloseResolutionType.ExternalResolution.ToString(), Value = "Tự xử lý bên ngoài (admin ghi chú cách xử lý)" }
            ],
            HandlingMethods = Enum.GetValues<ExternalDispositionType>()
                .Select(method => new MetadataDto
                {
                    Key = method.ToString(),
                    Value = ExternalDispositionMetadata.GetLabel(method)
                })
                .ToList()
        };

        return Task.FromResult(response);
    }
}
