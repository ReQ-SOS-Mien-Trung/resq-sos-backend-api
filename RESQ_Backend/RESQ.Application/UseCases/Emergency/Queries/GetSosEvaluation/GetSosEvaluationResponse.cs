using System.Text.Json;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosEvaluation;

/// <summary>
/// Chi ti?t di?m dánh giá rule-based (t? d?ng khi g?i SOS).
/// </summary>
public class SosRuleEvaluationDto
{
    /// <summary>ID b?n ghi dánh giá trong DB</summary>
    public int Id { get; set; }
    /// <summary>ID version config dã du?c snapshot khi ch?m di?m.</summary>
    public int? ConfigId { get; set; }
    /// <summary>config_version dã du?c snapshot khi ch?m di?m.</summary>
    public string? ConfigVersion { get; set; }

    // --- Giá tr? tuong thích legacy ---
    /// <summary>Ði?m y t? theo rule V1.</summary>
    public double MedicalScore { get; set; }
    /// <summary>Mirror legacy: hi?n ph?n ánh supply_urgency_score d? gi? tuong thích d? li?u cu.</summary>
    public double InjuryScore { get; set; }
    /// <summary>Mirror legacy: hi?n ph?n ánh vulnerability_score d? gi? tuong thích d? li?u cu.</summary>
    public double MobilityScore { get; set; }
    /// <summary>Mirror legacy: hi?n ph?n ánh situation_multiplier d? gi? tuong thích d? li?u cu.</summary>
    public double EnvironmentScore { get; set; }
    /// <summary>Mirror legacy: hi?n ph?n ánh relief_score d? gi? tuong thích d? li?u cu.</summary>
    public double FoodScore { get; set; }

    // --- T?ng h?p ---
    /// <summary>Ði?m t?ng theo expression priority_score c?a config version dã áp d?ng.</summary>
    public double TotalScore { get; set; }
    /// <summary>M?c uu tiên n?i b?: Low / Medium / High / Critical, tuong ?ng P4 / P3 / P2 / P1.</summary>
    public string PriorityLevel { get; set; } = string.Empty;
    /// <summary>Phiên b?n b? quy t?c du?c áp d?ng</summary>
    public string RuleVersion { get; set; } = string.Empty;
    /// <summary>Danh sách v?t ph?m/thi?t b? du?c d? xu?t c?n mang d?n</summary>
    public List<string> ItemsNeeded { get; set; } = [];
    /// <summary>Breakdown d?y d? theo config snapshot dã áp d?ng.</summary>
    public SosPriorityEvaluationDetails? Breakdown { get; set; }
    /// <summary>Th?i di?m dánh giá</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Chi ti?t m?t b?n phân tích AI (có th? có nhi?u l?n phân tích cho cùng m?t SOS).
/// </summary>
public class SosAiAnalysisDto
{
    public int Id { get; set; }
    /// <summary>Tên model AI (vd: gemini-2.0-flash)</summary>
    public string? ModelName { get; set; }
    /// <summary>Phiên b?n model</summary>
    public string? ModelVersion { get; set; }
    /// <summary>Lo?i phân tích (vd: SOS_TRIAGE)</summary>
    public string? AnalysisType { get; set; }
    /// <summary>M?c d? nghiêm tr?ng do AI d? xu?t</summary>
    public string? SuggestedSeverityLevel { get; set; }
    /// <summary>M?c uu tiên do AI d? xu?t</summary>
    public string? SuggestedPriority { get; set; }
    /// <summary>Gi?i thích / lý do dánh giá t? AI</summary>
    public string? Explanation { get; set; }
    /// <summary>Ð? tin c?y (0.0–1.0)</summary>
    public double? ConfidenceScore { get; set; }
    /// <summary>Ph?m vi d? xu?t</summary>
    public string? SuggestionScope { get; set; }
    /// <summary>Metadata d?y d? t? AI (JSON raw)</summary>
    public JsonElement? Metadata { get; set; }
    /// <summary>Th?i di?m AI phân tích xong</summary>
    public DateTime? CreatedAt { get; set; }
    /// <summary>Th?i di?m d? xu?t AI du?c áp d?ng (n?u có)</summary>
    public DateTime? AdoptedAt { get; set; }
}

/// <summary>
/// Response t?ng h?p: dánh giá rule-based + dánh giá AI cho m?t SOS request.
/// </summary>
public class GetSosEvaluationResponse
{
    public int SosRequestId { get; set; }
    /// <summary>Lo?i SOS (RESCUE / MEDICAL / EVACUATION / SUPPLY ...)</summary>
    public string? SosType { get; set; }
    /// <summary>Tr?ng thái hi?n t?i c?a SOS request</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>M?c uu tiên t?ng h?p dang áp d?ng trên SOS request</summary>
    public string? CurrentPriorityLevel { get; set; }

    /// <summary>
    /// Ðánh giá rule-based (luôn t?n t?i ngay sau khi g?i SOS).
    /// Null n?u d? li?u b? m?t trong DB.
    /// </summary>
    public SosRuleEvaluationDto? RuleEvaluation { get; set; }

    /// <summary>
    /// T?t c? các b?n phân tích AI (x? lý b?t d?ng b? sau khi g?i SOS).
    /// Danh sách r?ng n?u AI chua phân tích xong.
    /// </summary>
    public List<SosAiAnalysisDto> AiAnalyses { get; set; } = [];

    /// <summary>Có ít nh?t m?t b?n phân tích AI chua?</summary>
    public bool HasAiAnalysis => AiAnalyses.Count > 0;
}
