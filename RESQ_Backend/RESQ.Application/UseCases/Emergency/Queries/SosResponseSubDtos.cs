using System.Text.Json.Serialization;

namespace RESQ.Application.UseCases.Emergency.Queries;

public class SosStructuredDataDto
{
    [JsonPropertyName("incident")]
    public SosIncidentDto? Incident { get; set; }

    [JsonPropertyName("group_needs")]
    public SosGroupNeedsDto? GroupNeeds { get; set; }

    [JsonPropertyName("victims")]
    public List<SosVictimDto>? Victims { get; set; }

    [JsonPropertyName("prepared_profiles")]
    public List<SosPreparedProfileDto>? PreparedProfiles { get; set; }
}

public class SosIncidentDto
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
    public SosPeopleCountDto? PeopleCount { get; set; }

    [JsonPropertyName("has_injured")]
    public bool? HasInjured { get; set; }

    [JsonPropertyName("others_are_stable")]
    public bool? OthersAreStable { get; set; }

    [JsonPropertyName("can_move")]
    public bool? CanMove { get; set; }

    [JsonPropertyName("need_medical")]
    public bool? NeedMedical { get; set; }

    [JsonPropertyName("has_pregnant_any")]
    public bool? HasPregnantAny { get; set; }

    [JsonPropertyName("other_medical_description")]
    public string? OtherMedicalDescription { get; set; }
}

public class SosGroupNeedsDto
{
    [JsonPropertyName("supplies")]
    public List<string>? Supplies { get; set; }

    [JsonPropertyName("water")]
    public SosWaterNeedDto? Water { get; set; }

    [JsonPropertyName("food")]
    public SosFoodNeedDto? Food { get; set; }

    [JsonPropertyName("blanket")]
    public SosBlanketNeedDto? Blanket { get; set; }

    [JsonPropertyName("medicine")]
    public SosMedicineNeedDto? Medicine { get; set; }

    [JsonPropertyName("clothing")]
    public SosClothingNeedDto? Clothing { get; set; }

    [JsonPropertyName("other_supply_description")]
    public string? OtherSupplyDescription { get; set; }
}

public class SosWaterNeedDto
{
    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("remaining")]
    public string? Remaining { get; set; }
}

public class SosFoodNeedDto
{
    [JsonPropertyName("duration")]
    public string? Duration { get; set; }
}

public class SosBlanketNeedDto
{
    [JsonPropertyName("is_cold_or_wet")]
    public bool? IsColdOrWet { get; set; }

    [JsonPropertyName("are_blankets_enough")]
    public bool? AreBlanketsEnough { get; set; }

    [JsonPropertyName("availability")]
    public string? Availability { get; set; }

    [JsonPropertyName("request_count")]
    public int? RequestCount { get; set; }
}

public class SosMedicineNeedDto
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

public class SosClothingNeedDto
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("needed_people_count")]
    public int? NeededPeopleCount { get; set; }
}

public class SosVictimDto
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
    public SosVictimIncidentStatusDto? IncidentStatus { get; set; }

    [JsonPropertyName("personal_needs")]
    public SosVictimPersonalNeedsDto? PersonalNeeds { get; set; }
}

public class SosVictimIncidentStatusDto
{
    [JsonPropertyName("is_injured")]
    public bool? IsInjured { get; set; }

    [JsonPropertyName("severity")]
    public string? Severity { get; set; }

    [JsonPropertyName("medical_issues")]
    public List<string>? MedicalIssues { get; set; }
}

public class SosVictimPersonalNeedsDto
{
    [JsonPropertyName("clothing")]
    public SosVictimClothingDto? Clothing { get; set; }

    [JsonPropertyName("diet")]
    public SosVictimDietDto? Diet { get; set; }
}

public class SosVictimClothingDto
{
    [JsonPropertyName("needed")]
    public bool? Needed { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }
}

public class SosVictimDietDto
{
    [JsonPropertyName("has_special_diet")]
    public bool? HasSpecialDiet { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class SosPreparedProfileDto
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

public class SosPeopleCountDto
{
    [JsonPropertyName("adult")]
    public int? Adult { get; set; }

    [JsonPropertyName("child")]
    public int? Child { get; set; }

    [JsonPropertyName("elderly")]
    public int? Elderly { get; set; }
}

public class SosNetworkMetadataDto
{
    [JsonPropertyName("hop_count")]
    public int? HopCount { get; set; }

    [JsonPropertyName("path")]
    public List<string>? Path { get; set; }
}

public class SosSenderInfoDto
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

public class SosReporterInfoDto
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

public class SosVictimInfoDto
{
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("user_name")]
    public string? UserName { get; set; }

    [JsonPropertyName("user_phone")]
    public string? UserPhone { get; set; }
}
