using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetMetadata;

public class GetExportPeriodTypesQueryHandler : IRequestHandler<GetExportPeriodTypesQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(GetExportPeriodTypesQuery request, CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<ExportPeriodType>()
            .Select(e => new MetadataDto
            {
                Key = e.ToString(),
                Value = e switch
                {
                    ExportPeriodType.ByDateRange => "Theo khoảng ngày tùy chọn",
                    ExportPeriodType.ByMonth     => "Theo tháng",
                    _                            => e.ToString()
                }
            }).ToList();

        return Task.FromResult(result);
    }
}
