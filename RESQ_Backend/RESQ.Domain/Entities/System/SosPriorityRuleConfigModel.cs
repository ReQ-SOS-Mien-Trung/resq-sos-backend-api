namespace RESQ.Domain.Entities.System;

/// <summary>
/// Cấu hình bộ quy tắc chấm điểm ưu tiên SOS - có thể chỉnh sửa qua giao diện admin.
/// </summary>
public class SosPriorityRuleConfigModel
{
    public int Id { get; set; }

    /// <summary>
    /// JSON: {"unconscious":5,"drowning":5,"breathingDifficulty":5,...}
    /// Trọng số cho từng loại vấn đề y tế.
    /// </summary>
    public string IssueWeightsJson { get; set; } = "{}";

    /// <summary>
    /// JSON: ["unconscious","drowning","breathingDifficulty","breathing_difficulty","chestPainStroke","chest_pain_stroke","severelyBleeding","severely_bleeding"]
    /// Danh sách issue kích hoạt cờ "severe" về y tế.
    /// </summary>
    public string MedicalSevereIssuesJson { get; set; } = "[]";

    /// <summary>
    /// JSON: {"child":1.4,"elderly":1.3,"adult":1.0}
    /// Hệ số nhân theo độ tuổi.
    /// </summary>
    public string AgeWeightsJson { get; set; } = "{}";

    /// <summary>
    /// JSON: {"rescue":30,"relief":20,"other":10}
    /// Điểm theo loại yêu cầu SOS.
    /// </summary>
    public string RequestTypeScoresJson { get; set; } = "{}";

    /// <summary>
    /// JSON array of objects: [{"keys":["flooding","flood"],"multiplier":1.5,"severe":true},...]
    /// Hệ số nhân theo tình huống.
    /// </summary>
    public string SituationMultipliersJson { get; set; } = "[]";

    /// <summary>
    /// JSON: {"critical":{"minScore":70,"requireSevere":true},"high":{"minScore":45,"requireSevere":true},"medium":{"minScore":25,"requireSevere":false}}
    /// Ngưỡng điểm để xác định mức độ ưu tiên.
    /// </summary>
    public string PriorityThresholdsJson { get; set; } = "{}";

    public DateTime UpdatedAt { get; set; }
}
