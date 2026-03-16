



using RESQ.Application.Services;
namespace RESQ.Application.UseCases.Operations.Queries.GetMissions;

internal class AiMetadata
{
    public string? OverallAssessment { get; set; }
    public string? EstimatedDuration { get; set; }
    public string? SpecialNotes { get; set; }
    public List<SuggestedResourceDto>? SuggestedResources { get; set; }
}
