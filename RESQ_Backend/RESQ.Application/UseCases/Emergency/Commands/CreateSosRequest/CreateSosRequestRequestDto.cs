using System.Text.Json.Serialization;

namespace RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;

public class CreateSosRequestRequestDto
{
    [JsonPropertyName("packet_id")]
    public Guid? PacketId { get; set; }

    [JsonPropertyName("ts")]
    public long? Timestamp { get; set; }

    [JsonPropertyName("location")]
    public LocationDto Location { get; set; } = null!;

    [JsonPropertyName("sos_type")]
    public string? SosType { get; set; }

    [JsonPropertyName("msg")]
    public string RawMessage { get; set; } = null!;

    [JsonPropertyName("structured_data")]
    public StructuredDataDto? StructuredData { get; set; }

    [JsonPropertyName("network_metadata")]
    public NetworkMetadataDto? NetworkMetadata { get; set; }
}

public class LocationDto
{
    [JsonPropertyName("lat")]
    public double Latitude { get; set; }

    [JsonPropertyName("lng")]
    public double Longitude { get; set; }

    [JsonPropertyName("accuracy")]
    public double? Accuracy { get; set; }
}

public class StructuredDataDto
{
    [JsonPropertyName("situation")]
    public string? Situation { get; set; }

    [JsonPropertyName("has_injured")]
    public bool? HasInjured { get; set; }

    [JsonPropertyName("medical_issues")]
    public List<string>? MedicalIssues { get; set; }

    [JsonPropertyName("others_are_stable")]
    public bool? OthersAreStable { get; set; }

    [JsonPropertyName("people_count")]
    public PeopleCountDto? PeopleCount { get; set; }

    [JsonPropertyName("can_move")]
    public bool? CanMove { get; set; }

    [JsonPropertyName("need_medical")]
    public bool? NeedMedical { get; set; }

    [JsonPropertyName("additional_description")]
    public string? AdditionalDescription { get; set; }
}

public class PeopleCountDto
{
    [JsonPropertyName("adult")]
    public int? Adult { get; set; }

    [JsonPropertyName("child")]
    public int? Child { get; set; }

    [JsonPropertyName("elderly")]
    public int? Elderly { get; set; }
}

public class NetworkMetadataDto
{
    [JsonPropertyName("hop_count")]
    public int? HopCount { get; set; }

    [JsonPropertyName("path")]
    public List<string>? Path { get; set; }
}