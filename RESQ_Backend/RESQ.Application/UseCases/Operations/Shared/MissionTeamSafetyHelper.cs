using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Shared;

public static class MissionTeamSafetyHelper
{
    private const int DefaultTimeoutMinutes = 120; // 2 hours

    /// <summary>
    /// Khởi tạo timeout an toàn ban đầu khi mission bắt đầu.
    /// Timeout = Now + (MissionDuration * 1.5)
    /// </summary>
    public static void InitializeSafetyTimeout(MissionTeamModel team, MissionModel mission)
    {
        team.SafetyLatestCheckInAt = DateTime.UtcNow;
        team.SafetyStatus = "Safe";

        int missionDuration = DefaultTimeoutMinutes;
        
        if (mission.ExpectedEndTime.HasValue && mission.StartTime.HasValue)
        {
            missionDuration = (int)(mission.ExpectedEndTime.Value - mission.StartTime.Value).TotalMinutes;
        }
        else if (mission.Activities.Any())
        {
            missionDuration = mission.Activities.Sum(a => a.EstimatedTime ?? 0);
        }

        if (missionDuration <= 0) missionDuration = DefaultTimeoutMinutes;

        var timeoutDuration = (int)Math.Ceiling(missionDuration * 1.5);
        team.SafetyTimeoutAt = DateTime.UtcNow.AddMinutes(timeoutDuration);
    }

    /// <summary>
    /// Gia hạn timeout an toàn khi có sự kiện (check-in, activity update).
    /// Timeout = Current Timeout + (ActivityDuration * 0.5)
    /// </summary>
    public static void ExtendSafetyTimeout(MissionTeamModel team, IEnumerable<MissionActivityModel> activities)
    {
        team.SafetyLatestCheckInAt = DateTime.UtcNow;
        team.SafetyStatus = "Safe";

        var currentActivity = activities.FirstOrDefault(a => a.Status == MissionActivityStatus.OnGoing);
        if (currentActivity == null)
        {
            currentActivity = activities
                .Where(a => a.Status == MissionActivityStatus.Planned || a.Status == MissionActivityStatus.PendingConfirmation)
                .OrderBy(a => a.Step)
                .FirstOrDefault();
        }

        int activityDuration = currentActivity?.EstimatedTime ?? DefaultTimeoutMinutes;
        var extensionMinutes = (int)Math.Ceiling(activityDuration * 0.5);

        // Nếu timeout cũ đã qua hoặc chưa có, lấy Now làm mốc để cộng
        var baseTime = (team.SafetyTimeoutAt.HasValue && team.SafetyTimeoutAt > DateTime.UtcNow) 
            ? team.SafetyTimeoutAt.Value 
            : DateTime.UtcNow;

        team.SafetyTimeoutAt = baseTime.AddMinutes(extensionMinutes);
    }
}
