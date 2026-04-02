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

    [JsonPropertyName("reporter_info")]
    public ReporterInfoDto? ReporterInfo { get; set; }

    [JsonPropertyName("victim_info")]
    public VictimInfoDto? VictimInfo { get; set; }

    [JsonPropertyName("is_sent_on_behalf")]
    public bool? IsSentOnBehalf { get; set; }
}

public class LocationDto
{
    [JsonPropertyName("lat")]
    public double Latitude { get; set; }

    [JsonPropertyName("lng")]
    public double Longitude { get; set; }

    [JsonPropertyName("accuracy")]
    public double? Accuracy { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }
}

public class StructuredDataDto
{
    [JsonPropertyName("incident")]
    public IncidentDto? Incident { get; set; }

    [JsonPropertyName("group_needs")]
    public GroupNeedsDto? GroupNeeds { get; set; }

    [JsonPropertyName("victims")]
    public List<VictimItemDto>? Victims { get; set; }

    [JsonPropertyName("prepared_profiles")]
    public List<PreparedProfileItemDto>? PreparedProfiles { get; set; }
}

public class IncidentDto
{
    [JsonPropertyName("situation")]
    public string? Situation { get; set; }

    [JsonPropertyName("other_situation_description")]
    public string? OtherSituationDescription { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("additional_description")]
    public string? AdditionalDescription { get; set; }

    [JsonPropertyName("people_count")]
    public PeopleCountDto? PeopleCount { get; set; }

    [JsonPropertyName("has_injured")]
    public bool? HasInjured { get; set; }

    [JsonPropertyName("others_are_stable")]
    public bool? OthersAreStable { get; set; }

    [JsonPropertyName("can_move")]
    public bool? CanMove { get; set; }

    [JsonPropertyName("need_medical")]
    public bool? NeedMedical { get; set; }

    [JsonPropertyName("other_medical_description")]
    public string? OtherMedicalDescription { get; set; }
}

public class GroupNeedsDto
{
    [JsonPropertyName("supplies")]
    public List<string>? Supplies { get; set; }

    [JsonPropertyName("water")]
    public WaterNeedDto? Water { get; set; }

    [JsonPropertyName("food")]
    public FoodNeedDto? Food { get; set; }

    [JsonPropertyName("blanket")]
    public BlanketNeedDto? Blanket { get; set; }

    [JsonPropertyName("medicine")]
    public MedicineNeedDto? Medicine { get; set; }

    [JsonPropertyName("clothing")]
    public ClothingNeedDto? Clothing { get; set; }

    [JsonPropertyName("other_supply_description")]
    public string? OtherSupplyDescription { get; set; }
}

public class WaterNeedDto
{
    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("remaining")]
    public string? Remaining { get; set; }
}

public class FoodNeedDto
{
    [JsonPropertyName("duration")]
    public string? Duration { get; set; }
}

public class BlanketNeedDto
{
    [JsonPropertyName("is_cold_or_wet")]
    public bool? IsColdOrWet { get; set; }

    [JsonPropertyName("availability")]
    public string? Availability { get; set; }

    [JsonPropertyName("request_count")]
    public int? RequestCount { get; set; }
}

public class MedicineNeedDto
{
    [JsonPropertyName("needs_urgent_medicine")]
    public bool? NeedsUrgentMedicine { get; set; }

    [JsonPropertyName("conditions")]
    public List<string>? Conditions { get; set; }

    [JsonPropertyName("other_description")]
    public string? OtherDescription { get; set; }

    [JsonPropertyName("medical_needs")]
    public List<string>? MedicalNeeds { get; set; }

    [JsonPropertyName("medical_description")]
    public string? MedicalDescription { get; set; }
}

public class ClothingNeedDto
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

public class VictimItemDto
{
    [JsonPropertyName("person_id")]
    public string? PersonId { get; set; }

    [JsonPropertyName("person_type")]
    public string? PersonType { get; set; }

    [JsonPropertyName("index")]
    public int? Index { get; set; }

    [JsonPropertyName("custom_name")]
    public string? CustomName { get; set; }

    [JsonPropertyName("person_phone")]
    public string? PersonPhone { get; set; }

    [JsonPropertyName("incident_status")]
    public VictimIncidentStatusDto? IncidentStatus { get; set; }

    [JsonPropertyName("personal_needs")]
    public VictimPersonalNeedsDto? PersonalNeeds { get; set; }
}

public class VictimIncidentStatusDto
{
    [JsonPropertyName("is_injured")]
    public bool? IsInjured { get; set; }

    [JsonPropertyName("severity")]
    public string? Severity { get; set; }

    [JsonPropertyName("medical_issues")]
    public List<string>? MedicalIssues { get; set; }
}

public class VictimPersonalNeedsDto
{
    [JsonPropertyName("clothing")]
    public VictimClothingNeedDto? Clothing { get; set; }

    [JsonPropertyName("diet")]
    public VictimDietNeedDto? Diet { get; set; }
}

public class VictimClothingNeedDto
{
    [JsonPropertyName("needed")]
    public bool? Needed { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }
}

public class VictimDietNeedDto
{
    [JsonPropertyName("has_special_diet")]
    public bool? HasSpecialDiet { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class PreparedProfileItemDto
{
    [JsonPropertyName("profile_id")]
    public string? ProfileId { get; set; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("phone_number")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("person_type")]
    public string? PersonType { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }

    [JsonPropertyName("relation_group")]
    public string? RelationGroup { get; set; }

    [JsonPropertyName("medical_profile")]
    public object? MedicalProfile { get; set; }

    [JsonPropertyName("medical_baseline_note")]
    public string? MedicalBaselineNote { get; set; }

    [JsonPropertyName("special_needs_note")]
    public string? SpecialNeedsNote { get; set; }

    [JsonPropertyName("special_diet_note")]
    public string? SpecialDietNote { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }
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

public class ReporterInfoDto
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

public class VictimInfoDto
{
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("user_name")]
    public string? UserName { get; set; }

    [JsonPropertyName("user_phone")]
    public string? UserPhone { get; set; }
}