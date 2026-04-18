namespace RESQ.Domain.Enum.System;

/// <summary>
/// Phân loại mục đích sử dụng của prompt AI.
/// Mỗi loại chỉ được có duy nhất một prompt đang IsActive = true tại một thời điểm.
/// </summary>
public enum PromptType
{
    /// <summary>Phân tích và đánh giá độ ưu tiên yêu cầu SOS.</summary>
    SosPriorityAnalysis = 1,

    /// <summary>Lập kế hoạch điều phối nhiệm vụ cứu hộ.</summary>
    MissionPlanning = 2,
    MissionRequirementsAssessment = 3,
    MissionDepotPlanning = 4,
    MissionTeamPlanning = 5,
    MissionPlanValidation = 6
}
