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
            new() { Key = AssemblyPointStatus.Created.ToString(),          Value = "M?i t?o" },
            new() { Key = AssemblyPointStatus.Active.ToString(),           Value = "Ðang ho?t d?ng" },
            new() { Key = AssemblyPointStatus.Unavailable.ToString(), Value = "Ðang b?o trì" },
            new() { Key = AssemblyPointStatus.Closed.ToString(),           Value = "Ðã dóng" }
        };

        return await Task.FromResult(result);
    }
}
