using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.Services;

/// <summary>
/// Background task item for AI analysis
/// </summary>
public sealed record SosAiAnalysisTask(
    int SosRequestId,
    string? StructuredData,
    string? RawMessage,
    string? SosType,
    string ContentFingerprint,
    double RuleBasedScore,
    string? RuleBasedPriority,
    string? RuleVersion,
    int? RuleConfigId,
    string? RuleConfigVersion,
    string? RuleBreakdownJson,
    DateTime QueuedAtUtc)
{
    public static SosAiAnalysisTask Create(
        int sosRequestId,
        string? structuredData,
        string? rawMessage,
        string? sosType,
        SosRuleEvaluationModel ruleEvaluation)
    {
        return new SosAiAnalysisTask(
            sosRequestId,
            structuredData,
            rawMessage,
            sosType,
            BuildContentFingerprint(structuredData, rawMessage, sosType),
            ruleEvaluation.TotalScore,
            ruleEvaluation.PriorityLevel.ToString(),
            ruleEvaluation.RuleVersion,
            ruleEvaluation.ConfigId,
            ruleEvaluation.ConfigVersion,
            ruleEvaluation.BreakdownJson ?? ruleEvaluation.DetailsJson,
            DateTime.UtcNow);
    }

    public static string BuildContentFingerprint(
        string? structuredData,
        string? rawMessage,
        string? sosType)
    {
        var payload = JsonSerializer.Serialize(new
        {
            structuredData = NormalizeJson(structuredData),
            rawMessage = Normalize(rawMessage),
            sosType = Normalize(sosType)
        });

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Normalize a JSON string to canonical form with sorted keys.
    /// PostgreSQL jsonb sorts object keys alphabetically, so we must do the same
    /// to ensure fingerprints match regardless of original key ordering.
    /// </summary>
    private static string? NormalizeJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            using var doc = JsonDocument.Parse(value);
            return SerializeCanonical(doc.RootElement);
        }
        catch
        {
            return value.Trim();
        }
    }

    /// <summary>
    /// Recursively serialize a JsonElement with object keys sorted alphabetically (ordinal).
    /// This produces identical output regardless of the original key order in the JSON source.
    /// </summary>
    private static string SerializeCanonical(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var properties = element.EnumerateObject()
                    .OrderBy(p => p.Name, StringComparer.Ordinal)
                    .Select(p => $"{JsonSerializer.Serialize(p.Name)}:{SerializeCanonical(p.Value)}");
                return "{" + string.Join(",", properties) + "}";

            case JsonValueKind.Array:
                var items = element.EnumerateArray()
                    .Select(SerializeCanonical);
                return "[" + string.Join(",", items) + "]";

            default:
                return element.GetRawText();
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Queue for background AI analysis tasks
/// </summary>
public interface ISosAiAnalysisQueue
{
    /// <summary>
    /// Queue a task for background AI analysis
    /// </summary>
    ValueTask QueueAsync(SosAiAnalysisTask task);
}
