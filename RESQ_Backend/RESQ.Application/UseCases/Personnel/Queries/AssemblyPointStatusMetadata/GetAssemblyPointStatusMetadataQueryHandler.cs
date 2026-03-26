using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.AssemblyPointStatusMetadata;

public class GetAssemblyPointStatusMetadataQueryHandler
    : IRequestHandler<GetAssemblyPointStatusMetadataQuery, List<MetadataDto>>
{
    public async Task<List<MetadataDto>> Handle(
        GetAssemblyPointStatusMetadataQuery request,
        CancellationToken cancellationToken)
    {
        // Define metadata for UI dropdowns
        var result = new List<MetadataDto>
        {
            new() { Key = AssemblyPointStatus.Created.ToString(),          Value = "Mới tạo" },
            new() { Key = AssemblyPointStatus.Active.ToString(),           Value = "Đang hoạt động" },
            new() { Key = AssemblyPointStatus.Overloaded.ToString(),       Value = "Quá tải" },
            new() { Key = AssemblyPointStatus.UnderMaintenance.ToString(), Value = "Đang bảo trì" },
            new() { Key = AssemblyPointStatus.Closed.ToString(),           Value = "Đã đóng" }
        };

        return await Task.FromResult(result);
    }
}
