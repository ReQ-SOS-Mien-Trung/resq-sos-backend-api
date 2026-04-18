using MediatR;
using RESQ.Application.Common.Models;
using RESQ.Domain.Enum.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.PromptMetadata;

public record GetPromptTypesMetadataQuery : IRequest<List<MetadataDto>>;

public class GetPromptTypesMetadataQueryHandler
    : IRequestHandler<GetPromptTypesMetadataQuery, List<MetadataDto>>
{
    public Task<List<MetadataDto>> Handle(
        GetPromptTypesMetadataQuery request,
        CancellationToken cancellationToken)
    {
        var result = Enum.GetValues<PromptType>()
            .Select(promptType => new MetadataDto
            {
                Key = promptType.ToString(),
                Value = promptType switch
                {
                    PromptType.SosPriorityAnalysis => "Phân tích ưu tiên SOS",
                    PromptType.MissionPlanning => "Lập kế hoạch nhiệm vụ",
                    PromptType.MissionRequirementsAssessment => "Đánh giá nhu cầu nhiệm vụ",
                    PromptType.MissionDepotPlanning => "Lập kế hoạch kho",
                    PromptType.MissionTeamPlanning => "Lập kế hoạch đội cứu hộ",
                    PromptType.MissionPlanValidation => "Kiểm tra kế hoạch nhiệm vụ",
                    _ => promptType.ToString()
                }
            })
            .ToList();

        return Task.FromResult(result);
    }
}
