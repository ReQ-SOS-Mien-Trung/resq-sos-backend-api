//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using RESQ.Application.Repositories.Base;
//using RESQ.Application.Repositories.Personnel;
//using RESQ.Domain.Enum.Personnel;
//using RESQ.Infrastructure.Persistence.Context;

//namespace RESQ.Infrastructure.Services.Personnel;

//public class TeamInvitationExpirationBackgroundService : BackgroundService
//{
//    private readonly ILogger<TeamInvitationExpirationBackgroundService> _logger;
//    private readonly IServiceProvider _serviceProvider;

//    public TeamInvitationExpirationBackgroundService(
//        ILogger<TeamInvitationExpirationBackgroundService> logger,
//        IServiceProvider serviceProvider)
//    {
//        _logger = logger;
//        _serviceProvider = serviceProvider;
//    }

//    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//    {
//        _logger.LogInformation("Team Invitation Expiration Background Service is starting.");

//        while (!stoppingToken.IsCancellationRequested)
//        {
//            try
//            {
//                await ProcessExpiredInvitationsAsync(stoppingToken);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error occurred while processing expired team invitations.");
//            }

//            // Run periodically (e.g., every 30 minutes)
//            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
//        }
//    }

//    private async Task ProcessExpiredInvitationsAsync(CancellationToken stoppingToken)
//    {
//        using var scope = _serviceProvider.CreateScope();
//        var context = scope.ServiceProvider.GetRequiredService<ResQDbContext>();
//        var teamRepository = scope.ServiceProvider.GetRequiredService<IRescueTeamRepository>();
//        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

//        var expirationThreshold = DateTime.UtcNow.AddHours(-24);

//        // Find distinct TeamIds that have at least one expired pending member
//        var expiredTeamIds = await context.RescueTeamMembers
//            .Where(m => m.Status == TeamMemberStatus.Pending.ToString() && m.InvitedAt <= expirationThreshold)
//            .Select(m => m.TeamId)
//            .Distinct()
//            .ToListAsync(stoppingToken);

//        if (!expiredTeamIds.Any())
//            return;

//        int processedCount = 0;

//        foreach (var teamId in expiredTeamIds)
//        {
//            var team = await teamRepository.GetByIdAsync(teamId, stoppingToken);
//            if (team == null) continue;

//            // Find members in the domain model who are pending and expired
//            var expiredMembers = team.Members
//                .Where(m => m.Status == TeamMemberStatus.Pending && m.InvitedAt <= expirationThreshold)
//                .ToList();

//            foreach (var member in expiredMembers)
//            {
//                // Relying on domain logic to properly decline and evaluate overall Team state (e.g., AwaitingAcceptance -> Ready)
//                team.DeclineInvitation(member.UserId);
//                processedCount++;
//            }

//            await teamRepository.UpdateAsync(team, stoppingToken);
//        }

//        await unitOfWork.SaveAsync();
//        _logger.LogInformation("Auto-declined {Count} expired team invitations across {TeamCount} teams.", processedCount, expiredTeamIds.Count);
//    }
//}
