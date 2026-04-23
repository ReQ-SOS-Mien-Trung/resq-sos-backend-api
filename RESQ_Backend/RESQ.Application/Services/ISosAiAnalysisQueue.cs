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
    /// Normalize a JSON string by parsing and re-serializing to canonical form.
    /// This ensures fingerprints match regardless of whitespace or key ordering
    /// differences (e.g. PostgreSQL jsonb normalization).
    /// </summary>
    private static string? NormalizeJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            using var doc = JsonDocument.Parse(value);
            return JsonSerializer.Serialize(doc.RootElement);
        }
        catch
        {
            return value.Trim();
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
