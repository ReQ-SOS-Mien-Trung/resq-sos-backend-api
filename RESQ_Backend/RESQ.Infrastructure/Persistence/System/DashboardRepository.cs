using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.System;
using RESQ.Application.UseCases.SystemConfig.Queries.GetAdminTeamDetail;
using RESQ.Application.UseCases.SystemConfig.Queries.GetAdminTeamList;
using RESQ.Application.UseCases.SystemConfig.Queries.GetRescuerMissionScores;
using RESQ.Application.UseCases.SystemConfig.Queries.GetVictimsByPeriod;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Entities.Personnel;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.System;

public class DashboardRepository(ResQDbContext context) : IDashboardRepository
{
    private readonly ResQDbContext _context = context;

    /// <inheritdoc/>
    public async Task<List<VictimsByPeriodDto>> GetVictimsByPeriodAsync(
        DateTime from,
        DateTime to,
        string granularity,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            WITH valid_requests AS MATERIALIZED (
                SELECT structured_data, received_at
                FROM sos_requests
                WHERE received_at >= @from
                  AND received_at <= @to
                  AND structured_data IS NOT NULL
                  AND structured_data::text <> ''
                  AND structured_data::text <> 'null'
            )
            SELECT
                date_trunc(@granularity, received_at) AS "Period",
                COALESCE(SUM(
                    COALESCE((structured_data -> 'incident' -> 'people_count' ->> 'adult')::int,
                             (structured_data -> 'people_count' ->> 'adult')::int, 0)
                  + COALESCE((structured_data -> 'incident' -> 'people_count' ->> 'child')::int,
                             (structured_data -> 'people_count' ->> 'child')::int, 0)
                  + COALESCE((structured_data -> 'incident' -> 'people_count' ->> 'elderly')::int,
                             (structured_data -> 'people_count' ->> 'elderly')::int, 0)
                ), 0)::int AS "TotalVictims"
            FROM valid_requests
            GROUP BY date_trunc(@granularity, received_at)
            ORDER BY date_trunc(@granularity, received_at)
            """;

        var fromParam = new Npgsql.NpgsqlParameter("from", from);
        var toParam = new Npgsql.NpgsqlParameter("to", to);
        var granularityParam = new Npgsql.NpgsqlParameter("granularity", granularity);

        var results = await _context.Database
            .SqlQueryRaw<VictimsByPeriodRaw>(sql, fromParam, toParam, granularityParam)
            .ToListAsync(cancellationToken);

        return results.Select(r => new VictimsByPeriodDto
        {
            Period = r.Period,
            TotalVictims = r.TotalVictims
        }).ToList();
    }

    // Internal projection class for raw SQL result
    private sealed class VictimsByPeriodRaw
    {
        public DateTime Period { get; set; }
        public int TotalVictims { get; set; }
    }

    /// <inheritdoc/>
    public async Task<(int currentCount, int previousCount)> GetRescuerDailyCountsAsync(
        DateTime today,
        DateTime yesterday,
        CancellationToken cancellationToken = default)
    {
        var todayStart = today.ToUniversalTime();
        var todayEnd = todayStart.AddDays(1);

        var yesterdayStart = yesterday.ToUniversalTime();
        var yesterdayEnd = yesterdayStart.AddDays(1);

        var currentCount = await _context.Set<User>()
            .Where(u => u.RoleId == 3
                && u.CreatedAt >= todayStart
                && u.CreatedAt < todayEnd)
            .CountAsync(cancellationToken);

        var previousCount = await _context.Set<User>()
            .Where(u => u.RoleId == 3
                && u.CreatedAt >= yesterdayStart
                && u.CreatedAt < yesterdayEnd)
            .CountAsync(cancellationToken);

        return (currentCount, previousCount);
    }

    /// <inheritdoc/>
    public async Task<(int todayCompleted, int todayTotal, int yesterdayCompleted, int yesterdayTotal)> GetMissionFinishedCountsAsync(
        DateTime today,
        DateTime yesterday,
        CancellationToken cancellationToken = default)
    {
        var todayStart = today.ToUniversalTime();
        var todayEnd = todayStart.AddDays(1);

        var yesterdayStart = yesterday.ToUniversalTime();
        var yesterdayEnd = yesterdayStart.AddDays(1);

        var todayMissions = await _context.Set<Mission>()
            .Where(m => m.CompletedAt >= todayStart && m.CompletedAt < todayEnd
                && (m.Status == "Completed" || m.Status == "Incompleted"))
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Completed = g.Count(m => m.Status == "Completed"),
                Total = g.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        var yesterdayMissions = await _context.Set<Mission>()
            .Where(m => m.CompletedAt >= yesterdayStart && m.CompletedAt < yesterdayEnd
                && (m.Status == "Completed" || m.Status == "Incompleted"))
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Completed = g.Count(m => m.Status == "Completed"),
                Total = g.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return (
            todayMissions?.Completed ?? 0,
            todayMissions?.Total ?? 0,
            yesterdayMissions?.Completed ?? 0,
            yesterdayMissions?.Total ?? 0
        );
    }

    /// <inheritdoc/>
    public async Task<(int todayCount, int yesterdayCount)> GetSosRequestDailyCountsAsync(
        DateTime today,
        DateTime yesterday,
        CancellationToken cancellationToken = default)
    {
        var todayStart = today.ToUniversalTime();
        var todayEnd = todayStart.AddDays(1);

        var yesterdayStart = yesterday.ToUniversalTime();
        var yesterdayEnd = yesterdayStart.AddDays(1);

        var todayCount = await _context.Set<SosRequest>()
            .Where(s => s.ReceivedAt >= todayStart && s.ReceivedAt < todayEnd)
            .CountAsync(cancellationToken);

        var yesterdayCount = await _context.Set<SosRequest>()
            .Where(s => s.ReceivedAt >= yesterdayStart && s.ReceivedAt < yesterdayEnd)
            .CountAsync(cancellationToken);

        return (todayCount, yesterdayCount);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AdminTeamListItemDto>> GetAdminTeamListAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<RescueTeam>()
            .Include(t => t.AssemblyPoint)
            .Include(t => t.RescueTeamMembers)
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var teams = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = teams.Select(t => new AdminTeamListItemDto
        {
            Id = t.Id,
            Code = t.Code ?? string.Empty,
            Name = t.Name ?? string.Empty,
            TeamType = t.TeamType ?? string.Empty,
            Status = t.Status ?? string.Empty,
            AssemblyPointId = t.AssemblyPointId,
            AssemblyPointName = t.AssemblyPoint?.Name,
            MaxMembers = t.MaxMembers ?? 0,
            CurrentMemberCount = t.RescueTeamMembers.Count(m =>
                m.Status.ToLower() != "removed"),
            CreatedAt = t.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = t.UpdatedAt
        }).ToList();

        return new PagedResult<AdminTeamListItemDto>(items, totalCount, pageNumber, pageSize);
    }

    /// <inheritdoc/>
    public async Task<AdminTeamDetailDto?> GetAdminTeamDetailAsync(
        int teamId,
        CancellationToken cancellationToken = default)
    {
        var team = await _context.Set<RescueTeam>()
            .Include(t => t.AssemblyPoint)
            .Include(t => t.RescueTeamMembers)
                .ThenInclude(m => m.User)
                    .ThenInclude(u => u!.RescuerProfile)
            .Include(t => t.Coordinator)
            .FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);

        if (team is null) return null;

        // Load all mission_teams for this rescue team, with missions + activities
        var missionTeams = await _context.Set<MissionTeam>()
            .Include(mt => mt.Mission)
                .ThenInclude(m => m!.MissionActivities)
            .Include(mt => mt.MissionTeamReport)
            .Where(mt => mt.RescuerTeamId == teamId)
            .OrderByDescending(mt => mt.AssignedAt)
            .ToListAsync(cancellationToken);

        // Build pie chart
        var finishedMissions = missionTeams
            .Where(mt => mt.Mission != null &&
                (mt.Mission.Status == "Completed" || mt.Mission.Status == "Incompleted"))
            .ToList();
        var completedCount = finishedMissions.Count(mt => mt.Mission!.Status == "Completed");
        var incompletedCount = finishedMissions.Count(mt => mt.Mission!.Status == "Incompleted");
        var totalFinished = completedCount + incompletedCount;
        var completionRate = new MissionCompletionRateDto
        {
            TotalMissions = missionTeams.Count,
            CompletedCount = completedCount,
            IncompletedCount = incompletedCount,
            CompletedPercent = totalFinished > 0 ? Math.Round((double)completedCount / totalFinished * 100, 2) : 0,
            IncompletedPercent = totalFinished > 0 ? Math.Round((double)incompletedCount / totalFinished * 100, 2) : 0
        };

        var managerName = team.Coordinator != null
            ? $"{team.Coordinator.LastName} {team.Coordinator.FirstName}".Trim()
            : null;

        return new AdminTeamDetailDto
        {
            Id = team.Id,
            Code = team.Code ?? string.Empty,
            Name = team.Name ?? string.Empty,
            TeamType = team.TeamType ?? string.Empty,
            Status = team.Status ?? string.Empty,
            AssemblyPointId = team.AssemblyPointId,
            AssemblyPointName = team.AssemblyPoint?.Name,
            ManagedByName = managerName,
            MaxMembers = team.MaxMembers ?? 0,
            CreatedAt = team.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = team.UpdatedAt,
            Members = team.RescueTeamMembers.Select(m => new AdminTeamMemberDto
            {
                UserId = m.UserId,
                FirstName = m.User?.FirstName,
                LastName = m.User?.LastName,
                Phone = m.User?.Phone,
                Email = m.User?.Email,
                AvatarUrl = m.User?.AvatarUrl,
                RescuerType = m.User?.RescuerProfile?.RescuerType,
                Status = m.Status,
                IsLeader = m.IsLeader,
                RoleInTeam = m.RoleInTeam,
                JoinedAt = m.RespondedAt ?? m.InvitedAt
            }).ToList(),
            Missions = missionTeams.Select(mt => new AdminTeamMissionDto
            {
                MissionTeamId = mt.Id,
                MissionId = mt.MissionId ?? 0,
                MissionStatus = mt.Mission?.Status ?? string.Empty,
                MissionType = mt.Mission?.MissionType,
                TeamAssignmentStatus = mt.Status ?? string.Empty,
                AssignedAt = mt.AssignedAt,
                UnassignedAt = mt.UnassignedAt,
                MissionStartTime = mt.Mission?.StartTime,
                MissionCompletedAt = mt.Mission?.CompletedAt,
                IsCompleted = mt.Mission?.IsCompleted,
                ReportStatus = mt.MissionTeamReport?.ReportStatus,
                Activities = mt.Mission?.MissionActivities
                    .OrderBy(a => a.Step)
                    .Select(a => new AdminMissionActivityDto
                    {
                        Id = a.Id,
                        Step = a.Step,
                        ActivityType = a.ActivityType,
                        Description = a.Description,
                        Status = a.Status,
                        AssignedAt = a.AssignedAt,
                        CompletedAt = a.CompletedAt
                    }).ToList() ?? []
            }).ToList(),
            CompletionRate = completionRate
        };
    }

    /// <inheritdoc/>
    public async Task<RescuerMissionScoresDto?> GetRescuerMissionScoresAsync(
        Guid rescuerId,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Set<User>()
            .Include(u => u.RescuerProfile)
            .FirstOrDefaultAsync(u => u.Id == rescuerId, cancellationToken);

        if (user is null) return null;

        // Overall score from rescuer_scores table
        var overallScore = await _context.Set<RescuerScore>()
            .FirstOrDefaultAsync(s => s.UserId == rescuerId, cancellationToken);

        // Per-mission evaluations: join evaluation → report → mission_team → mission
        var evaluations = await _context.Set<MissionTeamMemberEvaluation>()
            .Include(e => e.MissionTeamReport)
                .ThenInclude(r => r!.MissionTeam)
                    .ThenInclude(mt => mt!.Mission)
            .Where(e => e.RescuerId == rescuerId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        // Team membership history
        var memberships = await _context.Set<RescueTeamMember>()
            .Include(m => m.Team)
            .Where(m => m.UserId == rescuerId)
            .OrderByDescending(m => m.InvitedAt)
            .ToListAsync(cancellationToken);

        OverallScoreDto? overallDto = null;
        if (overallScore != null)
        {
            overallDto = new OverallScoreDto
            {
                OverallAverageScore = overallScore.OverallAverageScore,
                EvaluationCount = overallScore.EvaluationCount,
                ResponseTimeScore = overallScore.ResponseTimeScore,
                RescueEffectivenessScore = overallScore.RescueEffectivenessScore,
                DecisionHandlingScore = overallScore.DecisionHandlingScore,
                SafetyMedicalSkillScore = overallScore.SafetyMedicalSkillScore,
                TeamworkCommunicationScore = overallScore.TeamworkCommunicationScore
            };
        }

        var missionEvals = evaluations.Select(e =>
        {
            var avg = (e.ResponseTimeScore + e.RescueEffectivenessScore +
                       e.DecisionHandlingScore + e.SafetyMedicalSkillScore +
                       e.TeamworkCommunicationScore) / 5m;
            return new MissionEvaluationDto
            {
                EvaluationId = e.Id,
                MissionTeamReportId = e.MissionTeamReportId,
                MissionId = e.MissionTeamReport?.MissionTeam?.MissionId ?? 0,
                MissionType = e.MissionTeamReport?.MissionTeam?.Mission?.MissionType,
                MissionCompletedAt = e.MissionTeamReport?.MissionTeam?.Mission?.CompletedAt,
                ResponseTimeScore = e.ResponseTimeScore,
                RescueEffectivenessScore = e.RescueEffectivenessScore,
                DecisionHandlingScore = e.DecisionHandlingScore,
                SafetyMedicalSkillScore = e.SafetyMedicalSkillScore,
                TeamworkCommunicationScore = e.TeamworkCommunicationScore,
                AverageScore = Math.Round(avg, 2),
                EvaluatedAt = e.CreatedAt
            };
        }).ToList();

        var teamHistory = memberships.Select(m => new TeamMembershipHistoryDto
        {
            TeamId = m.TeamId,
            TeamCode = m.Team?.Code,
            TeamName = m.Team?.Name,
            TeamType = m.Team?.TeamType,
            Status = m.Status,
            IsLeader = m.IsLeader,
            RoleInTeam = m.RoleInTeam,
            JoinedAt = m.RespondedAt ?? m.InvitedAt,
            LeftAt = m.Status.ToLower() == "removed" ? m.Team?.DisbandAt ?? m.RespondedAt : null
        }).ToList();

        return new RescuerMissionScoresDto
        {
            RescuerId = rescuerId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            AvatarUrl = user.AvatarUrl,
            OverallScore = overallDto,
            MissionEvaluations = missionEvals,
            TeamHistory = teamHistory
        };
    }
}
