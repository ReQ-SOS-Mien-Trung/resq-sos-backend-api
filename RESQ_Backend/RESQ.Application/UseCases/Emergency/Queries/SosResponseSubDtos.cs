using System.Text.Json.Serialization;

namespace RESQ.Application.UseCases.Emergency.Queries;

public class SosStructuredDataDto
{
    [JsonPropertyName("address")]
    public string? Address { get; set; }

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
    public SosPeopleCountDto? PeopleCount { get; set; }

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
    public List<SosInjuredPersonDto>? InjuredPersons { get; set; }

    [JsonPropertyName("supply_details")]
    public SosSupplyDetailsDto? SupplyDetails { get; set; }
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

public class SosInjuredPersonDto
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

public class SosSupplyDetailsDto
{
    [JsonPropertyName("are_blankets_enough")]
    public bool? AreBlanketsEnough { get; set; }

    [JsonPropertyName("blanket_request_count")]
    public int? BlanketRequestCount { get; set; }

    [JsonPropertyName("clothing_persons")]
    public List<SosClothingPersonDto>? ClothingPersons { get; set; }

    [JsonPropertyName("food_duration")]
    public string? FoodDuration { get; set; }

    [JsonPropertyName("medical_description")]
    public string? MedicalDescription { get; set; }

    [JsonPropertyName("medical_needs")]
    public List<string>? MedicalNeeds { get; set; }

    [JsonPropertyName("special_diet_persons")]
    public List<SosSpecialDietPersonDto>? SpecialDietPersons { get; set; }

    [JsonPropertyName("water_duration")]
    public string? WaterDuration { get; set; }

    [JsonPropertyName("water_remaining")]
    public string? WaterRemaining { get; set; }
}

public class SosClothingPersonDto
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

public class SosSpecialDietPersonDto
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

public class SosVictimInfoDto
{
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("user_name")]
    public string? UserName { get; set; }

    [JsonPropertyName("user_phone")]
    public string? UserPhone { get; set; }
}
