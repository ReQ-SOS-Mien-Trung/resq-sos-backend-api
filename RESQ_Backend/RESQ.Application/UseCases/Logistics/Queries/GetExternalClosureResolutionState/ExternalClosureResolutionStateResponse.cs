namespace RESQ.Application.UseCases.Logistics.Queries.GetExternalClosureResolutionState;

public class ExternalClosureResolutionStateResponse
{
    public int DepotId { get; set; }
    public int? ClosureId { get; set; }
    public bool HasActiveExternalResolution { get; set; }
    public bool CanDownloadExternalTemplate { get; set; }
    public bool CanUploadExternalResolution { get; set; }
    public string? ClosureStatus { get; set; }
    public string? ResolutionType { get; set; }
    public string? ExternalNote { get; set; }
    public int RemainingItemCount { get; set; }
}
