using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;
using RESQ.Domain.Entities.Logistics.ValueObjects;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Services.Operations;

public class MissionTeamSafetyMonitorBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MissionTeamSafetyMonitorBackgroundService> _logger;

    public MissionTeamSafetyMonitorBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<MissionTeamSafetyMonitorBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CheckSafetyTimeoutsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while executing safety monitor background service.");
            }
        }
    }

    private async Task CheckSafetyTimeoutsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ResQDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var now = DateTime.UtcNow;

        var atRiskTeams = await dbContext.MissionTeams
            .Include(t => t.RescuerTeam)
            .Include(t => t.MissionTeamMembers)
                .ThenInclude(m => m.Rescuer)
            .Where(t => t.SafetyStatus == "Safe" 
                        && t.SafetyTimeoutAt <= now 
                        && dbContext.Missions.Any(m => m.Id == t.MissionId && m.Status == "OnGoing"))
            .ToListAsync(cancellationToken);

        if (atRiskTeams.Count == 0)
            return;

        foreach (var team in atRiskTeams)
        {
            try
            {
                var leader = team.MissionTeamMembers.FirstOrDefault(m => m.RoleInTeam == "LEADER") 
                             ?? team.MissionTeamMembers.FirstOrDefault();
                
                if (leader == null || leader.RescuerId == null)
                {
                    _logger.LogWarning("No members found for MissionTeam {TeamId}. Cannot create SOS.", team.Id);
                    team.SafetyStatus = "AtRisk";
                    await dbContext.SaveChangesAsync(cancellationToken);
                    continue;
                }

                if (team.CurrentLocation == null)
                {
                    _logger.LogWarning("No location for MissionTeam {TeamId}. Cannot create SOS. Marking as AtRisk.", team.Id);
                    team.SafetyStatus = "AtRisk";
                    await dbContext.SaveChangesAsync(cancellationToken);
                    continue;
                }

                var lat = team.CurrentLocation.Y;
                var lng = team.CurrentLocation.X;

                var teamName = team.RescuerTeam?.Name ?? $"Team #{team.Id}";
                var teamCode = team.RescuerTeam?.Code;
                var sosMessage = $"Hệ thống tự động: Đội cứu hộ {(teamCode != null ? teamCode : teamName)} quá hạn báo cáo an toàn. Có nguy cơ mất liên lạc.";

                var structuredDataJson = JsonSerializer.Serialize(new
                {
                    incident = new
                    {
                        situation = "lost_contact",
                        additional_description = sosMessage,
                    },
                    team_incident_context = new
                    {
                        mission_id = team.MissionId,
                        mission_team_id = team.Id,
                        incident_type = "safety_timeout",
                        team_name = teamName,
                        team_code = teamCode,
                        location_source = team.LocationSource
                    },
                    operation_support = new
                    {
                        origin = "safety_monitor"
                    }
                });

                var reporterInfoJson = JsonSerializer.Serialize(new
                {
                    user_id = leader.RescuerId,
                    user_name = $"{leader.Rescuer?.FirstName} {leader.Rescuer?.LastName}".Trim(),
                    user_phone = leader.Rescuer?.Phone,
                    is_online = false
                });

                var victimInfoJson = JsonSerializer.Serialize(new
                {
                    user_id = leader.RescuerId,
                    user_name = $"{leader.Rescuer?.FirstName} {leader.Rescuer?.LastName}".Trim() ?? teamName,
                    user_phone = leader.Rescuer?.Phone
                });

                team.SafetyStatus = "SosCreated";
                await dbContext.SaveChangesAsync(cancellationToken);

                try
                {
                    var response = await mediator.Send(
                        new CreateSosRequestCommand(
                            leader.RescuerId.Value,
                            new GeoLocation(lat, lng),
                            sosMessage,
                            SosType: "RESCUE",
                            StructuredData: structuredDataJson,
                            VictimInfo: victimInfoJson,
                            ReporterInfo: reporterInfoJson),
                        cancellationToken);

                    team.GeneratedSosRequestId = response.Id;
                    
                    // Cập nhật trạng thái mission sang Incompleted vì team đã gặp nạn
                    var mission = await dbContext.Missions.FirstOrDefaultAsync(m => m.Id == team.MissionId, cancellationToken);
                    if (mission != null)
                    {
                        mission.Status = "Incompleted";
                        mission.CompletedAt = DateTime.UtcNow;
                    }

                    await dbContext.SaveChangesAsync(cancellationToken);
                    
                    _logger.LogInformation("Created automatic safety SOS #{SosRequestId} for MissionTeamId={MissionTeamId}. Mission marked as Failed.", response.Id, team.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create SOS request using mediator for MissionTeamId={MissionTeamId}. Reverting status.", team.Id);
                    team.SafetyStatus = "Safe";
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing safety timeout for MissionTeamId={MissionTeamId}", team.Id);
            }
        }
    }
}
