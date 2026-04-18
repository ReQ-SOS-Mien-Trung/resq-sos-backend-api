using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace RESQ.Application.Common
{
    public static class RelativeProfileNormalizer
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static string? NullIfEmpty(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        public static string NormalizeTags(IEnumerable<string>? tags)
        {
            if (tags == null)
                return "[]";

            var normalized = tags
                .Select(t => t?.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!)
                .GroupBy(t => t.ToLowerInvariant())
                .Select(g => g.First())
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList();

            return JsonSerializer.Serialize(normalized);
        }

        public static List<string> DeserializeTags(string? tagsJson)
        {
            if (string.IsNullOrWhiteSpace(tagsJson))
                return new List<string>();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(tagsJson) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static object? DeserializeMedicalProfile(string? medicalProfileJson)
        {
            if (string.IsNullOrWhiteSpace(medicalProfileJson) || medicalProfileJson == "{}")
                return null;

            try
            {
                return JsonSerializer.Deserialize<object>(medicalProfileJson);
            }
            catch
            {
                return null;
            }
        }

        public static (string displayName, string? phoneNumber, string personType, string relationGroup,
                       string tagsJson, string? medicalBaselineNote, string? specialNeedsNote, string? specialDietNote,
                       string? gender, string medicalProfileJson)
            Normalize(
                string displayName,
                string? phoneNumber,
                string personType,
                string relationGroup,
                IEnumerable<string>? tags,
                string? medicalBaselineNote,
                string? specialNeedsNote,
                string? specialDietNote,
                string? gender = null,
                string? medicalProfileJson = null)
        {
            return (
                displayName.Trim(),
                NullIfEmpty(phoneNumber),
                personType.Trim(),
                relationGroup.Trim(),
                NormalizeTags(tags),
                NullIfEmpty(medicalBaselineNote),
                NullIfEmpty(specialNeedsNote),
                NullIfEmpty(specialDietNote),
                NullIfEmpty(gender),
                string.IsNullOrWhiteSpace(medicalProfileJson) ? "{}" : medicalProfileJson
            );
        }
    }
}
