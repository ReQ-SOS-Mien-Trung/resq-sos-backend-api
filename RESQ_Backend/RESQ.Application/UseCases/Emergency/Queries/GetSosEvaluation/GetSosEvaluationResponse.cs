using System.Text.Json;
using RESQ.Domain.Entities.Emergency;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosEvaluation;

/// <summary>
/// Chi tiết điểm đánh giá rule-based (tự động khi gửi SOS).
/// </summary>
public class SosRuleEvaluationDto
{
    /// <summary>ID bản ghi đánh giá trong DB</summary>
    public int Id { get; set; }

    // --- Giá trị tương thích legacy ---
    /// <summary>Điểm y tế theo rule V1.</summary>
    public double MedicalScore { get; set; }
    /// <summary>Mirror legacy: hiện phản ánh supply_urgency_score để giữ tương thích dữ liệu cũ.</summary>
    public double InjuryScore { get; set; }
    /// <summary>Mirror legacy: hiện phản ánh vulnerability_score để giữ tương thích dữ liệu cũ.</summary>
    public double MobilityScore { get; set; }
    /// <summary>Mirror legacy: hiện phản ánh situation_multiplier để giữ tương thích dữ liệu cũ.</summary>
    public double EnvironmentScore { get; set; }
    /// <summary>Mirror legacy: hiện phản ánh relief_score để giữ tương thích dữ liệu cũ.</summary>
    public double FoodScore { get; set; }

    // --- Tổng hợp ---
    /// <summary>Điểm tổng theo công thức V1: ROUND((medical_score + relief_score) * situation_multiplier).</summary>
    public double TotalScore { get; set; }
    /// <summary>Mức ưu tiên nội bộ: Low / Medium / High / Critical, tương ứng P4 / P3 / P2 / P1.</summary>
    public string PriorityLevel { get; set; } = string.Empty;
    /// <summary>Phiên bản bộ quy tắc được áp dụng</summary>
    public string RuleVersion { get; set; } = string.Empty;
    /// <summary>Danh sách vật tư/thiết bị được đề xuất cần mang đến</summary>
    public List<string> ItemsNeeded { get; set; } = [];
    /// <summary>Breakdown đầy đủ theo rulebase V1.</summary>
    public SosPriorityEvaluationDetails? Breakdown { get; set; }
    /// <summary>Thời điểm đánh giá</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Chi tiết một bản phân tích AI (có thể có nhiều lần phân tích cho cùng một SOS).
/// </summary>
public class SosAiAnalysisDto
{
    public int Id { get; set; }
    /// <summary>Tên model AI (vd: gemini-2.0-flash)</summary>
    public string? ModelName { get; set; }
    /// <summary>Phiên bản model</summary>
    public string? ModelVersion { get; set; }
    /// <summary>Loại phân tích (vd: SOS_TRIAGE)</summary>
    public string? AnalysisType { get; set; }
    /// <summary>Mức độ nghiêm trọng do AI đề xuất</summary>
    public string? SuggestedSeverityLevel { get; set; }
    /// <summary>Mức ưu tiên do AI đề xuất</summary>
    public string? SuggestedPriority { get; set; }
    /// <summary>Giải thích / lý do đánh giá từ AI</summary>
    public string? Explanation { get; set; }
    /// <summary>Độ tin cậy (0.0–1.0)</summary>
    public double? ConfidenceScore { get; set; }
    /// <summary>Phạm vi đề xuất</summary>
    public string? SuggestionScope { get; set; }
    /// <summary>Metadata đầy đủ từ AI (JSON raw)</summary>
    public JsonElement? Metadata { get; set; }
    /// <summary>Thời điểm AI phân tích xong</summary>
    public DateTime? CreatedAt { get; set; }
    /// <summary>Thời điểm đề xuất AI được áp dụng (nếu có)</summary>
    public DateTime? AdoptedAt { get; set; }
}

/// <summary>
/// Response tổng hợp: đánh giá rule-based + đánh giá AI cho một SOS request.
/// </summary>
public class GetSosEvaluationResponse
{
    public int SosRequestId { get; set; }
    /// <summary>Loại SOS (RESCUE / MEDICAL / EVACUATION / SUPPLY ...)</summary>
    public string? SosType { get; set; }
    /// <summary>Trạng thái hiện tại của SOS request</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Mức ưu tiên tổng hợp đang áp dụng trên SOS request</summary>
    public string? CurrentPriorityLevel { get; set; }

    /// <summary>
    /// Đánh giá rule-based (luôn tồn tại ngay sau khi gửi SOS).
    /// Null nếu dữ liệu bị mất trong DB.
    /// </summary>
    public SosRuleEvaluationDto? RuleEvaluation { get; set; }

    /// <summary>
    /// Tất cả các bản phân tích AI (xử lý bất đồng bộ sau khi gửi SOS).
    /// Danh sách rỗng nếu AI chưa phân tích xong.
    /// </summary>
    public List<SosAiAnalysisDto> AiAnalyses { get; set; } = [];

    /// <summary>Có ít nhất một bản phân tích AI chưa?</summary>
    public bool HasAiAnalysis => AiAnalyses.Count > 0;
}
