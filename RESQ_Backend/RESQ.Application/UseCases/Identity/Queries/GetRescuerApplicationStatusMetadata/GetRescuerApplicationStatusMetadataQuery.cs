using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Identity;

namespace RESQ.Application.UseCases.Identity.Queries.GetRescuerApplicationStatusMetadata;

public record GetRescuerApplicationStatusMetadataQuery : IRequest<List<MetadataDto>>;

public class GetRescuerApplicationStatusMetadataQueryHandler : IRequestHandler<GetRescuerApplicationStatusMetadataQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetRescuerApplicationStatusMetadataQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<RescuerApplicationStatus>()
            .Select(status => new MetadataDto
            {
                Key = status.ToString(),
                Value = status switch
                {
                    RescuerApplicationStatus.Pending => "Chờ duyệt",
                    RescuerApplicationStatus.Approved => "Đã duyệt",
                    RescuerApplicationStatus.Rejected => "Đã từ chối",
                    _ => status.ToString()
                }
            })
            .ToList();

        return Task.FromResult(result);
    }
}
