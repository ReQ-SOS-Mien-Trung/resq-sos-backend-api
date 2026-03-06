using System.Text.Json;

namespace RESQ.Application.UseCases.Emergency.Queries.GetSosEvaluation;

/// <summary>
/// Chi tiết điểm đánh giá rule-based (tự động khi gửi SOS).
/// </summary>
public class SosRuleEvaluationDto
{
    /// <summary>ID bản ghi đánh giá trong DB</summary>
    public int Id { get; set; }

    // --- Điểm thành phần ---
    /// <summary>Điểm y tế (0–100): dựa trên NeedMedical, MedicalIssues</summary>
    public double MedicalScore { get; set; }
    /// <summary>Điểm chấn thương (0–100): dựa trên HasInjured, OthersAreStable</summary>
    public double InjuryScore { get; set; }
    /// <summary>Điểm di chuyển (0–100): dựa trên CanMove</summary>
    public double MobilityScore { get; set; }
    /// <summary>Điểm môi trường (0–100): dựa trên SosType và Situation</summary>
    public double EnvironmentScore { get; set; }
    /// <summary>Điểm lương thực / nhu yếu phẩm (0–100): dựa trên PeopleCount</summary>
    public double FoodScore { get; set; }

    // --- Tổng hợp ---
    /// <summary>Điểm tổng (weighted average): Medical×30% + Injury×25% + Mobility×15% + Environment×20% + Food×10%</summary>
    public double TotalScore { get; set; }
    /// <summary>Mức ưu tiên: Low / Medium / High / Critical</summary>
    public string PriorityLevel { get; set; } = string.Empty;
    /// <summary>Phiên bản bộ quy tắc được áp dụng</summary>
    public string RuleVersion { get; set; } = string.Empty;
    /// <summary>Danh sách vật tư/thiết bị được đề xuất cần mang đến</summary>
    public List<string> ItemsNeeded { get; set; } = [];
    /// <summary>Thời điểm đánh giá</summary>
    public DateTime CreatedAt { get; set; }

    // --- Ngưỡng tham chiếu ---
    /// <summary>Ngưỡng điểm tương ứng từng mức ưu tiên (thông tin tham khảo)</summary>
    public PriorityThresholdsDto PriorityThresholds { get; } = new();
    /// <summary>Trọng số từng thành phần (thông tin tham khảo)</summary>
    public ScoreWeightsDto ScoreWeights { get; } = new();
}

public class PriorityThresholdsDto
{
    public string Critical { get; init; } = "≥ 70";
    public string High { get; init; } = "50 – 69";
    public string Medium { get; init; } = "30 – 49";
    public string Low { get; init; } = "< 30";
}

public class ScoreWeightsDto
{
    public string Medical { get; init; } = "30%";
    public string Injury { get; init; } = "25%";
    public string Mobility { get; init; } = "15%";
    public string Environment { get; init; } = "20%";
    public string Food { get; init; } = "10%";
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
