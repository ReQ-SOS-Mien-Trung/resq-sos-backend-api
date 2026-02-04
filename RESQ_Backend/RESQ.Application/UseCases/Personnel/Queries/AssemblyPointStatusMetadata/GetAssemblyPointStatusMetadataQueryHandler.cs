using MediatR;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.AssemblyPointStatusMetadata;

public class GetAssemblyPointStatusMetadataQueryHandler
    : IRequestHandler<GetAssemblyPointStatusMetadataQuery, List<AssemblyPointStatusMetadataDto>>
{
    public async Task<List<AssemblyPointStatusMetadataDto>> Handle(
        GetAssemblyPointStatusMetadataQuery request,
        CancellationToken cancellationToken)
    {
        // Define metadata for UI dropdowns
        var result = new List<AssemblyPointStatusMetadataDto>
        {
            new() { Key = AssemblyPointStatus.Active.ToString(), Label = "Đang hoạt động" },
            new() { Key = AssemblyPointStatus.Overloaded.ToString(), Label = "Quá tải" },
            new() { Key = AssemblyPointStatus.Unavailable.ToString(), Label = "Tạm ngưng hoạt động" }
        };

        return await Task.FromResult(result);
    }
}
