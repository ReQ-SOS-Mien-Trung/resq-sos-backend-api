namespace RESQ.Application.UseCases.Operations.Shared;

/// <summary>
/// Internal context built by the normalization helper and consumed by
/// <see cref="TeamIncidentAssistanceSosHelper"/> to create a support SOS request.
/// A null value means no SOS should be created.
/// </summary>
internal sealed record IncidentSosCreationContext(
    IReadOnlyList<string> SupportTypes,
    string? Priority,
    string? EvacuationPriority,
    string? MeetupPoint,
    bool HasInjured,
    IReadOnlyList<string>? MedicalIssues,
    int? AdultCount,
    IReadOnlyList<string>? AffectedResources,
    string? AdditionalDescription,
    double? Latitude,
    double? Longitude,
    string? ReportedIncidentType);
