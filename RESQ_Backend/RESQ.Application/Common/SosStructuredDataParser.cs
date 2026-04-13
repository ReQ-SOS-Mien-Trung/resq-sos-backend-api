using System.Text.Json;
using RESQ.Application.UseCases.Emergency.Queries;

namespace RESQ.Application.Common;

/// <summary>
/// Dual-read parser for SOS structured_data JSON.
/// Handles both new nested format (with "incident" key) and legacy flat format.
/// </summary>
public static class SosStructuredDataParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Parse structured_data JSON into the new nested DTO shape.
    /// If the data uses the new format (has "incident" key), deserializes directly.
    /// If the data uses the old flat format, maps it to the nested shape.
    /// </summary>
    public static SosStructuredDataDto? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("incident", out _))
            {
                // New nested format - deserialize directly
                return JsonSerializer.Deserialize<SosStructuredDataDto>(json, JsonOptions);
            }

            // Old flat format - map to nested shape
            return MapFlatToNested(root, json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parse reporter_info from its own column, falling back to sender_info for legacy records.
    /// </summary>
    public static SosReporterInfoDto? ParseReporterInfo(string? reporterInfoJson, string? senderInfoJson)
    {
        var source = !string.IsNullOrWhiteSpace(reporterInfoJson) ? reporterInfoJson : senderInfoJson;
        if (string.IsNullOrWhiteSpace(source)) return null;

        try
        {
            return JsonSerializer.Deserialize<SosReporterInfoDto>(source, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static SosStructuredDataDto MapFlatToNested(JsonElement root, string json)
    {
        // Deserialize flat data using a lenient helper class
        var flat = JsonSerializer.Deserialize<FlatStructuredData>(json, JsonOptions);
        if (flat == null) return new SosStructuredDataDto();

        var incident = new SosIncidentDto
        {
            Situation = flat.Situation,
            OtherSituationDescription = flat.OtherSituationDescription,
            Address = flat.Address,
            AdditionalDescription = flat.AdditionalDescription,
            PeopleCount = flat.PeopleCount != null ? new SosPeopleCountDto
            {
                Adult = flat.PeopleCount.Adult,
                Child = flat.PeopleCount.Child,
                Elderly = flat.PeopleCount.Elderly
            } : null,
            HasInjured = flat.HasInjured,
            OthersAreStable = flat.OthersAreStable,
            CanMove = flat.CanMove,
            NeedMedical = flat.NeedMedical,
            HasPregnantAny = flat.HasPregnantAny,
            OtherMedicalDescription = flat.OtherMedicalDescription
        };

        var groupNeeds = new SosGroupNeedsDto
        {
            Supplies = flat.Supplies,
            OtherSupplyDescription = flat.OtherSupplyDescription,
            Water = flat.SupplyDetails != null ? new SosWaterNeedDto
            {
                Duration = flat.SupplyDetails.WaterDuration,
                Remaining = flat.SupplyDetails.WaterRemaining
            } : null,
            Food = flat.SupplyDetails != null ? new SosFoodNeedDto
            {
                Duration = flat.SupplyDetails.FoodDuration
            } : null,
            Blanket = flat.SupplyDetails != null ? new SosBlanketNeedDto
            {
                AreBlanketsEnough = flat.SupplyDetails.AreBlanketsEnough,
                RequestCount = flat.SupplyDetails.BlanketRequestCount
            } : null,
            Medicine = flat.SupplyDetails != null ? new SosMedicineNeedDto
            {
                MedicalNeeds = flat.SupplyDetails.MedicalNeeds,
                MedicalDescription = flat.SupplyDetails.MedicalDescription
            } : null,
            Clothing = flat.SupplyDetails != null ? new SosClothingNeedDto
            {
                NeededPeopleCount = flat.SupplyDetails.ClothingNeededPeopleCount
            } : null
        };

        var victims = flat.InjuredPersons?.Select(ip => new SosVictimDto
        {
            PersonType = ip.PersonType,
            Index = ip.Index,
            CustomName = ip.CustomName ?? ip.Name,
            IncidentStatus = new SosVictimIncidentStatusDto
            {
                IsInjured = true,
                Severity = ip.Severity,
                MedicalIssues = ip.MedicalIssues
            }
        }).ToList();

        return new SosStructuredDataDto
        {
            Incident = incident,
            GroupNeeds = groupNeeds,
            Victims = victims,
            PreparedProfiles = new List<SosPreparedProfileDto>()
        };
    }

    #region Legacy flat format classes (private)

    private class FlatStructuredData
    {
        public string? Address { get; set; }
        public string? Situation { get; set; }
        public string? OtherSituationDescription { get; set; }
        public bool? HasInjured { get; set; }
        public List<string>? MedicalIssues { get; set; }
        public string? OtherMedicalDescription { get; set; }
        public bool? OthersAreStable { get; set; }
        public FlatPeopleCount? PeopleCount { get; set; }
        public bool? CanMove { get; set; }
        public bool? NeedMedical { get; set; }
        public bool? HasPregnantAny { get; set; }
        public List<string>? Supplies { get; set; }
        public string? OtherSupplyDescription { get; set; }
        public string? AdditionalDescription { get; set; }
        public List<FlatInjuredPerson>? InjuredPersons { get; set; }
        public FlatSupplyDetails? SupplyDetails { get; set; }
    }

    private class FlatPeopleCount
    {
        public int? Adult { get; set; }
        public int? Child { get; set; }
        public int? Elderly { get; set; }
    }

    private class FlatInjuredPerson
    {
        public int? Index { get; set; }
        public string? Name { get; set; }
        public string? CustomName { get; set; }
        public string? PersonType { get; set; }
        public List<string>? MedicalIssues { get; set; }
        public string? Severity { get; set; }
    }

    private class FlatSupplyDetails
    {
        public bool? AreBlanketsEnough { get; set; }
        public int? BlanketRequestCount { get; set; }
        public int? ClothingNeededPeopleCount { get; set; }
        public string? FoodDuration { get; set; }
        public string? MedicalDescription { get; set; }
        public List<string>? MedicalNeeds { get; set; }
        public string? WaterDuration { get; set; }
        public string? WaterRemaining { get; set; }
    }

    #endregion
}
