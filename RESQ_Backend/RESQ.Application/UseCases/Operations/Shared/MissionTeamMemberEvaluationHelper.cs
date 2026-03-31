using RESQ.Application.Exceptions;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Operations.Shared;

internal static class MissionTeamMemberEvaluationHelper
{
    internal static IReadOnlyDictionary<Guid, MissionTeamMemberInfo> GetEvaluableMembers(MissionTeamModel missionTeam)
    {
        return missionTeam.RescueTeamMembers
            .Where(member =>
                !member.IsLeader &&
                string.Equals(member.Status, TeamMemberStatus.Accepted.ToString(), StringComparison.OrdinalIgnoreCase))
            .GroupBy(member => member.UserId)
            .ToDictionary(group => group.Key, group => group.First());
    }

    internal static void ValidateDraft(
        IReadOnlyCollection<MissionTeamMemberEvaluationModel> evaluations,
        MissionTeamModel missionTeam,
        Guid savedBy)
    {
        if (evaluations.Count == 0)
        {
            return;
        }

        var isLeader = missionTeam.RescueTeamMembers.Any(x => x.UserId == savedBy && x.IsLeader);
        if (!isLeader)
        {
            throw new ForbiddenException("Chỉ đội trưởng mới được lưu phần đánh giá thành viên.");
        }

        ValidateCore(evaluations, missionTeam, requireAllMembers: false);
    }

    internal static void ValidateSubmit(
        IReadOnlyCollection<MissionTeamMemberEvaluationModel> evaluations,
        MissionTeamModel missionTeam)
    {
        ValidateCore(evaluations, missionTeam, requireAllMembers: true);
    }

    private static void ValidateCore(
        IReadOnlyCollection<MissionTeamMemberEvaluationModel> evaluations,
        MissionTeamModel missionTeam,
        bool requireAllMembers)
    {
        var evaluableMembers = GetEvaluableMembers(missionTeam);

        var duplicateRescuerId = evaluations
            .GroupBy(x => x.RescuerId)
            .FirstOrDefault(group => group.Count() > 1)?
            .Key;

        if (duplicateRescuerId.HasValue)
        {
            throw new BadRequestException($"Rescuer {duplicateRescuerId.Value} bị đánh giá trùng trong cùng một báo cáo.");
        }

        var invalidRescuerId = evaluations
            .Select(x => x.RescuerId)
            .FirstOrDefault(rescuerId => !evaluableMembers.ContainsKey(rescuerId));

        if (invalidRescuerId != Guid.Empty)
        {
            throw new BadRequestException($"Rescuer {invalidRescuerId} không thuộc danh sách thành viên cần được đánh giá.");
        }

        foreach (var evaluation in evaluations)
        {
            ValidateScore(nameof(evaluation.ResponseTimeScore), evaluation.ResponseTimeScore, evaluation.RescuerId);
            ValidateScore(nameof(evaluation.RescueEffectivenessScore), evaluation.RescueEffectivenessScore, evaluation.RescuerId);
            ValidateScore(nameof(evaluation.DecisionHandlingScore), evaluation.DecisionHandlingScore, evaluation.RescuerId);
            ValidateScore(nameof(evaluation.SafetyMedicalSkillScore), evaluation.SafetyMedicalSkillScore, evaluation.RescuerId);
            ValidateScore(nameof(evaluation.TeamworkCommunicationScore), evaluation.TeamworkCommunicationScore, evaluation.RescuerId);
        }

        if (!requireAllMembers)
        {
            return;
        }

        var missingRescuerIds = evaluableMembers.Keys
            .Where(rescuerId => evaluations.All(x => x.RescuerId != rescuerId))
            .ToList();

        if (missingRescuerIds.Count > 0)
        {
            throw new BadRequestException("Đội trưởng phải đánh giá đầy đủ tất cả thành viên không giữ vai trò leader trước khi nộp báo cáo.");
        }
    }

    private static void ValidateScore(string scoreName, decimal scoreValue, Guid rescuerId)
    {
        if (scoreValue < 0m || scoreValue > 10m)
        {
            throw new BadRequestException($"{scoreName} của rescuer {rescuerId} phải nằm trong khoảng từ 0 đến 10.");
        }
    }
}
