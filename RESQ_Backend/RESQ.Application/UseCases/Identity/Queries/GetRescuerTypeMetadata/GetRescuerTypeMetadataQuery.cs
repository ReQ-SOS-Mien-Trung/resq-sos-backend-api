using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetRescuerTypeMetadata;

public record GetRescuerTypeMetadataQuery : IRequest<List<MetadataDto>>;

public class GetRescuerTypeMetadataQueryHandler : IRequestHandler<GetRescuerTypeMetadataQuery, List<MetadataDto>>
{
    public async Task<List<MetadataDto>> Handle(GetRescuerTypeMetadataQuery request, CancellationToken cancellationToken)
    {
        var result = new List<MetadataDto>
        {
            new() { Key = RescuerType.Core.ToString(), Value = "Nhân s? nòng c?t" },
            new() { Key = RescuerType.Volunteer.ToString(), Value = "Tình nguy?n viên" }
        };

        return await Task.FromResult(result);
    }
}
