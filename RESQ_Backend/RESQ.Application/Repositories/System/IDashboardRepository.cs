using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.SystemConfig.Queries.GetAdminTeamDetail;
using RESQ.Application.UseCases.SystemConfig.Queries.GetAdminTeamList;
using RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerMissionScores;
using RESQ.Application.UseCases.SystemConfig.Queries.GetVictimsByPeriod;

namespace RESQ.Application.Repositories.System;

public interface IDashboardRepository
{
    /// <summary>
    /// Trả về tổng số victim (adult + child + elderly từ structured_data)
    /// nhóm theo khoảng thời gian (day / week / month).
    /// </summary>
    Task<List<VictimsByPeriodDto>> GetVictimsByPeriodAsync(
        DateTime from,
        DateTime to,
        string granularity,
        CancellationToken cancellationToken = default);

    /// <summary>Trả về tổng số rescuer được tạo trong ngày hôm nay và hôm qua.</summary>
    Task<(int currentCount, int previousCount)> GetRescuerDailyCountsAsync(
        DateTime today,
        DateTime yesterday,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trả về số lượng mission Completed/Incompleted theo ngày hôm nay và hôm qua.
    /// </summary>
    Task<(int todayCompleted, int todayTotal, int yesterdayCompleted, int yesterdayTotal)> GetMissionFinishedCountsAsync(
        DateTime today,
        DateTime yesterday,
        CancellationToken cancellationToken = default);

    /// <summary>Trả về số lượng SOS request được tạo trong ngày hôm nay và hôm qua.</summary>
    Task<(int todayCount, int yesterdayCount)> GetSosRequestDailyCountsAsync(
        DateTime today,
        DateTime yesterday,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách đội cứu hộ có phân trang, sắp xếp theo UpdatedAt DESC (biến động mới nhất lên trước).
    /// </summary>
    Task<PagedResult<AdminTeamListItemDto>> GetAdminTeamListAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy chi tiết đội cứu hộ kèm missions, activities và tỉ lệ hoàn thành mission.
    /// Trả về null nếu không tìm thấy.
    /// </summary>
    Task<AdminTeamDetailDto?> GetAdminTeamDetailAsync(
        int teamId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy điểm theo từng lần đánh giá mission, overall score, avg per-criteria
    /// và lịch sử tham gia đội của một rescuer.
    /// Trả về null nếu không tìm thấy user.
    /// </summary>
    Task<RescuerMissionScoresDto?> GetRescuerMissionScoresAsync(
        Guid rescuerId,
        CancellationToken cancellationToken = default);
}
