using System.Text.Json.Serialization;

namespace RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;

public class CreateSosRequestRequestDto
{
    [JsonPropertyName("packet_id")]
    public Guid? PacketId { get; set; }

    [JsonPropertyName("origin_id")]
    public string? OriginId { get; set; }

    [JsonPropertyName("ts")]
    public long? Timestamp { get; set; }

    /// <summary>ISO-8601 datetime từ thiết bị (thời điểm SOS được tạo phía client).</summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

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

    [JsonPropertyName("sender_info")]
    public SenderInfoDto? SenderInfo { get; set; }
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

    [JsonPropertyName("other_situation_description")]
    public string? OtherSituationDescription { get; set; }

    [JsonPropertyName("has_injured")]
    public bool? HasInjured { get; set; }

    [JsonPropertyName("medical_issues")]
    public List<string>? MedicalIssues { get; set; }

    [JsonPropertyName("other_medical_description")]
    public string? OtherMedicalDescription { get; set; }

    [JsonPropertyName("others_are_stable")]
    public bool? OthersAreStable { get; set; }

    [JsonPropertyName("people_count")]
    public PeopleCountDto? PeopleCount { get; set; }

    [JsonPropertyName("can_move")]
    public bool? CanMove { get; set; }

    [JsonPropertyName("need_medical")]
    public bool? NeedMedical { get; set; }

    [JsonPropertyName("supplies")]
    public List<string>? Supplies { get; set; }

    [JsonPropertyName("other_supply_description")]
    public string? OtherSupplyDescription { get; set; }

    [JsonPropertyName("additional_description")]
    public string? AdditionalDescription { get; set; }

    [JsonPropertyName("injured_persons")]
    public List<InjuredPersonDto>? InjuredPersons { get; set; }

    [JsonPropertyName("supply_details")]
    public SupplyDetailsDto? SupplyDetails { get; set; }
}

public class InjuredPersonDto
{
    [JsonPropertyName("index")]
    public int? Index { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("custom_name")]
    public string? CustomName { get; set; }

    [JsonPropertyName("person_type")]
    public string? PersonType { get; set; }

    [JsonPropertyName("medical_issues")]
    public List<string>? MedicalIssues { get; set; }

    [JsonPropertyName("severity")]
    public string? Severity { get; set; }
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

public class SenderInfoDto
{
    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("user_name")]
    public string? UserName { get; set; }

    [JsonPropertyName("user_phone")]
    public string? UserPhone { get; set; }

    [JsonPropertyName("battery_level")]
    public int? BatteryLevel { get; set; }

    [JsonPropertyName("is_online")]
    public bool? IsOnline { get; set; }
}

public class SupplyDetailsDto
{
    [JsonPropertyName("are_blankets_enough")]
    public bool? AreBlanketsEnough { get; set; }

    [JsonPropertyName("blanket_request_count")]
    public int? BlanketRequestCount { get; set; }

    [JsonPropertyName("clothing_persons")]
    public List<ClothingPersonDto>? ClothingPersons { get; set; }

    [JsonPropertyName("food_duration")]
    public string? FoodDuration { get; set; }

    [JsonPropertyName("medical_description")]
    public string? MedicalDescription { get; set; }

    [JsonPropertyName("medical_needs")]
    public List<string>? MedicalNeeds { get; set; }

    [JsonPropertyName("special_diet_persons")]
    public List<SpecialDietPersonDto>? SpecialDietPersons { get; set; }

    [JsonPropertyName("water_duration")]
    public string? WaterDuration { get; set; }

    [JsonPropertyName("water_remaining")]
    public string? WaterRemaining { get; set; }
}

public class ClothingPersonDto
{
    [JsonPropertyName("index")]
    public int? Index { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("custom_name")]
    public string? CustomName { get; set; }

    [JsonPropertyName("person_type")]
    public string? PersonType { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }
}

public class SpecialDietPersonDto
{
    [JsonPropertyName("index")]
    public int? Index { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("custom_name")]
    public string? CustomName { get; set; }

    [JsonPropertyName("person_type")]
    public string? PersonType { get; set; }

    [JsonPropertyName("diet_description")]
    public string? DietDescription { get; set; }
}