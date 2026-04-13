using System.Text.Json;
using System.Text.Json.Serialization;
using RESQ.Domain.Entities.System;

namespace RESQ.Application.Common;

public static class SosStructuredDataValidationHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<string> Validate(string? structuredDataJson, SosPriorityRuleConfigDocument config)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(structuredDataJson))
        {
            return errors;
        }

        StructuredDataPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<StructuredDataPayload>(structuredDataJson, JsonOptions);
        }
        catch
        {
            errors.Add("structured_data không đúng định dạng JSON hợp lệ.");
            return errors;
        }

        var totalPeople = GetTotalPeople(payload);
        if (totalPeople < config.UiConstraints.MinTotalPeopleToProceed)
        {
            errors.Add($"Tổng số người trong structured_data phải lớn hơn hoặc bằng {config.UiConstraints.MinTotalPeopleToProceed}.");
        }

        var waterDuration = SosPriorityRuleConfigSupport.NormalizeKey(payload?.GroupNeeds?.Water?.Duration);
        if (!string.IsNullOrWhiteSpace(waterDuration)
            && !config.UiOptions.WaterDuration.Select(SosPriorityRuleConfigSupport.NormalizeKey)
                .Contains(waterDuration, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("group_needs.water.duration không thuộc danh sách WATER_DURATION hợp lệ.");
        }

        var foodDuration = SosPriorityRuleConfigSupport.NormalizeKey(payload?.GroupNeeds?.Food?.Duration);
        if (!string.IsNullOrWhiteSpace(foodDuration)
            && !config.UiOptions.FoodDuration.Select(SosPriorityRuleConfigSupport.NormalizeKey)
                .Contains(foodDuration, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("group_needs.food.duration không thuộc danh sách FOOD_DURATION hợp lệ.");
        }

        var maxPeopleBound = Math.Max(config.UiConstraints.BlanketRequestCountMin, totalPeople);
        var blanketRequestCount = payload?.GroupNeeds?.Blanket?.RequestCount;
        if (blanketRequestCount.HasValue
            && (blanketRequestCount.Value < config.UiConstraints.BlanketRequestCountMin || blanketRequestCount.Value > maxPeopleBound))
        {
            errors.Add($"group_needs.blanket.request_count phải nằm trong khoảng từ {config.UiConstraints.BlanketRequestCountMin} đến {maxPeopleBound}.");
        }

        var clothingNeededPeopleCount = payload?.GroupNeeds?.Clothing?.NeededPeopleCount;
        if (clothingNeededPeopleCount.HasValue
            && (clothingNeededPeopleCount.Value < 1 || clothingNeededPeopleCount.Value > maxPeopleBound))
        {
            errors.Add($"group_needs.clothing.needed_people_count phải nằm trong khoảng từ 1 đến {maxPeopleBound}.");
        }

        return errors;
    }

    private static int GetTotalPeople(StructuredDataPayload? payload)
    {
        if (payload?.Incident?.PeopleCount is { } peopleCount)
        {
            return Math.Max(0, peopleCount.Adult ?? 0)
                + Math.Max(0, peopleCount.Child ?? 0)
                + Math.Max(0, peopleCount.Elderly ?? 0);
        }

        return payload?.Victims?.Count ?? 0;
    }

    private sealed class StructuredDataPayload
    {
        [JsonPropertyName("incident")]
        public IncidentPayload? Incident { get; set; }

        [JsonPropertyName("group_needs")]
        public GroupNeedsPayload? GroupNeeds { get; set; }

        [JsonPropertyName("victims")]
        public List<object>? Victims { get; set; }
    }

    private sealed class IncidentPayload
    {
        [JsonPropertyName("people_count")]
        public PeopleCountPayload? PeopleCount { get; set; }
    }

    private sealed class PeopleCountPayload
    {
        [JsonPropertyName("adult")]
        public int? Adult { get; set; }

        [JsonPropertyName("child")]
        public int? Child { get; set; }

        [JsonPropertyName("elderly")]
        public int? Elderly { get; set; }
    }

    private sealed class GroupNeedsPayload
    {
        [JsonPropertyName("water")]
        public DurationPayload? Water { get; set; }

        [JsonPropertyName("food")]
        public DurationPayload? Food { get; set; }

        [JsonPropertyName("blanket")]
        public BlanketPayload? Blanket { get; set; }

        [JsonPropertyName("clothing")]
        public ClothingPayload? Clothing { get; set; }
    }

    private sealed class DurationPayload
    {
        [JsonPropertyName("duration")]
        public string? Duration { get; set; }
    }

    private sealed class BlanketPayload
    {
        [JsonPropertyName("request_count")]
        public int? RequestCount { get; set; }
    }

    private sealed class ClothingPayload
    {
        [JsonPropertyName("needed_people_count")]
        public int? NeededPeopleCount { get; set; }
    }
}
