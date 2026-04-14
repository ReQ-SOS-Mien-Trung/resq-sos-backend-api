using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Domain.Enum.Personnel;
using RESQ.Infrastructure.Entities.Personnel;
using RESQ.Infrastructure.Persistence.Base;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Personnel;

namespace RESQ.Tests.Infrastructure.Personnel;

public class AssemblyEventRepositoryTests
{
    [Fact]
    public async Task CheckOutAsync_MarksParticipantCheckedOutAndMirrorsTeamMember()
    {
        var userId = Guid.Parse("cccccccc-1111-1111-1111-111111111111");
        await using var context = CreateContext();
        var repository = CreateRepository(context);

        SeedAssemblyAttendance(context, userId, participantCheckedIn: true, participantCheckedOut: false, teamMemberCheckedIn: true);

        var success = await repository.CheckOutAsync(eventId: 1, userId);
        await context.SaveChangesAsync();

        Assert.True(success);
        var participant = await context.AssemblyParticipants.SingleAsync(p => p.RescuerId == userId);
        var member = await context.RescueTeamMembers.SingleAsync(m => m.UserId == userId);
        Assert.True(participant.IsCheckedOut);
        Assert.NotNull(participant.CheckOutTime);
        Assert.False(member.CheckedIn);
    }

    [Fact]
    public async Task CheckInAsync_WhenParticipantWasCheckedOut_MarksPresentAndMirrorsTeamMember()
    {
        var userId = Guid.Parse("cccccccc-2222-2222-2222-222222222222");
        await using var context = CreateContext();
        var repository = CreateRepository(context);

        SeedAssemblyAttendance(context, userId, participantCheckedIn: true, participantCheckedOut: true, teamMemberCheckedIn: false);

        var success = await repository.CheckInAsync(eventId: 1, userId);
        await context.SaveChangesAsync();

        Assert.True(success);
        var participant = await context.AssemblyParticipants.SingleAsync(p => p.RescuerId == userId);
        var member = await context.RescueTeamMembers.SingleAsync(m => m.UserId == userId);
        Assert.True(participant.IsCheckedIn);
        Assert.False(participant.IsCheckedOut);
        Assert.Null(participant.CheckOutTime);
        Assert.True(member.CheckedIn);
    }

    [Fact]
    public async Task ReturnCheckInAsync_CreatesParticipantWhenMissingAndMirrorsTeamMember()
    {
        var userId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");
        await using var context = CreateContext();
        var repository = CreateRepository(context);

        SeedAssemblyAttendance(context, userId, includeParticipant: false, teamMemberCheckedIn: false);

        var success = await repository.ReturnCheckInAsync(eventId: 1, userId);
        await context.SaveChangesAsync();

        Assert.True(success);
        var participant = await context.AssemblyParticipants.SingleAsync(p => p.RescuerId == userId);
        var member = await context.RescueTeamMembers.SingleAsync(m => m.UserId == userId);
        Assert.Equal(1, participant.AssemblyEventId);
        Assert.True(participant.IsCheckedIn);
        Assert.False(participant.IsCheckedOut);
        Assert.True(member.CheckedIn);
    }

    [Fact]
    public async Task ReadModels_ExcludeCheckedOutParticipantsFromCheckedInState()
    {
        var checkedInUserId = Guid.Parse("cccccccc-4444-4444-4444-444444444444");
        var checkedOutUserId = Guid.Parse("cccccccc-5555-5555-5555-555555555555");
        await using var context = CreateContext();
        var repository = CreateRepository(context);

        SeedAssemblyAttendance(context, checkedInUserId, participantCheckedIn: true, participantCheckedOut: false, teamMemberCheckedIn: true);
        context.AssemblyParticipants.Add(new AssemblyParticipant
        {
            Id = 2,
            AssemblyEventId = 1,
            RescuerId = checkedOutUserId,
            Status = AssemblyParticipantStatus.CheckedIn.ToString(),
            IsCheckedIn = true,
            IsCheckedOut = true,
            CheckInTime = DateTime.UtcNow.AddHours(-2),
            CheckOutTime = DateTime.UtcNow.AddHours(-1)
        });
        await context.SaveChangesAsync();

        var events = await repository.GetEventsByAssemblyPointAsync(assemblyPointId: 5, pageNumber: 1, pageSize: 10);
        var myEvents = await repository.GetAssemblyEventsForRescuerAsync(checkedOutUserId, pageNumber: 1, pageSize: 10);

        Assert.Equal(1, Assert.Single(events.Items).CheckedInCount);
        Assert.False(Assert.Single(myEvents.Items).IsCheckedIn);
    }

    private static ResQDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ResQDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ResQDbContext(options);
    }

    private static AssemblyEventRepository CreateRepository(ResQDbContext context)
    {
        var unitOfWork = new UnitOfWork(context, NullLogger<UnitOfWork>.Instance);
        return new AssemblyEventRepository(unitOfWork);
    }

    private static void SeedAssemblyAttendance(
        ResQDbContext context,
        Guid userId,
        bool includeParticipant = true,
        bool participantCheckedIn = true,
        bool participantCheckedOut = false,
        bool teamMemberCheckedIn = true)
    {
        context.AssemblyPoints.Add(new AssemblyPoint
        {
            Id = 5,
            Name = "AP 5",
            Status = AssemblyPointStatus.Active.ToString(),
            CreatedAt = DateTime.UtcNow
        });
        context.AssemblyEvents.Add(new AssemblyEvent
        {
            Id = 1,
            AssemblyPointId = 5,
            AssemblyDate = DateTime.UtcNow,
            Status = AssemblyEventStatus.Gathering.ToString(),
            CreatedAt = DateTime.UtcNow
        });

        if (includeParticipant)
        {
            context.AssemblyParticipants.Add(new AssemblyParticipant
            {
                Id = 1,
                AssemblyEventId = 1,
                RescuerId = userId,
                Status = participantCheckedIn
                    ? AssemblyParticipantStatus.CheckedIn.ToString()
                    : AssemblyParticipantStatus.Assigned.ToString(),
                IsCheckedIn = participantCheckedIn,
                IsCheckedOut = participantCheckedOut,
                CheckInTime = participantCheckedIn ? DateTime.UtcNow.AddHours(-2) : null,
                CheckOutTime = participantCheckedOut ? DateTime.UtcNow.AddHours(-1) : null
            });
        }

        context.RescueTeams.Add(new RescueTeam
        {
            Id = 10,
            AssemblyPointId = 5,
            Name = "Team 10",
            Status = RescueTeamStatus.Available.ToString(),
            CreatedAt = DateTime.UtcNow
        });
        context.RescueTeamMembers.Add(new RescueTeamMember
        {
            TeamId = 10,
            UserId = userId,
            Status = TeamMemberStatus.Accepted.ToString(),
            InvitedAt = DateTime.UtcNow.AddHours(-3),
            CheckedIn = teamMemberCheckedIn
        });

        context.SaveChanges();
    }
}
