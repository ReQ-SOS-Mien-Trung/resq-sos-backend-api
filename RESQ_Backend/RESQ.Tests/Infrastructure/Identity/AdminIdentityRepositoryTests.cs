using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Domain.Enum.Identity;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Persistence.Base;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Identity;

namespace RESQ.Tests.Infrastructure.Identity;

public class AdminIdentityRepositoryTests
{
    [Fact]
    public async Task UserRepository_GetPagedAsync_FiltersByRescuerType()
    {
        var options = new DbContextOptionsBuilder<ResQDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ResQDbContext(options);
        var coreUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var volunteerUserId = Guid.Parse("10000000-0000-0000-0000-000000000002");

        context.Users.AddRange(
            new User
            {
                Id = coreUserId,
                RoleId = 3,
                FirstName = "Core",
                LastName = "Rescuer",
                Username = "core.rescuer",
                Password = "hashed",
                Email = "core@example.com",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            },
            new User
            {
                Id = volunteerUserId,
                RoleId = 3,
                FirstName = "Volunteer",
                LastName = "Rescuer",
                Username = "volunteer.rescuer",
                Password = "hashed",
                Email = "volunteer@example.com",
                CreatedAt = DateTime.UtcNow
            });
        context.RescuerProfiles.AddRange(
            new RescuerProfile
            {
                UserId = coreUserId,
                RescuerType = "Core",
                IsEligibleRescuer = true
            },
            new RescuerProfile
            {
                UserId = volunteerUserId,
                RescuerType = "Volunteer",
                IsEligibleRescuer = true
            });
        await context.SaveChangesAsync();

        var repository = new UserRepository(new UnitOfWork(context, NullLogger<UnitOfWork>.Instance));

        var result = await repository.GetPagedAsync(
            pageNumber: 1,
            pageSize: 10,
            roleId: 3,
            isEligible: true,
            rescuerType: RescuerType.Core);

        var item = Assert.Single(result.Items);
        Assert.Equal(coreUserId, item.Id);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task RescuerApplicationRepository_GetPagedAsync_FiltersByEnumStatus()
    {
        var options = new DbContextOptionsBuilder<ResQDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ResQDbContext(options);
        var pendingUserId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var approvedUserId = Guid.Parse("20000000-0000-0000-0000-000000000002");

        context.Users.AddRange(
            new User
            {
                Id = pendingUserId,
                FirstName = "Pending",
                LastName = "User",
                Username = "pending.user",
                Password = "hashed",
                Email = "pending@example.com"
            },
            new User
            {
                Id = approvedUserId,
                FirstName = "Approved",
                LastName = "User",
                Username = "approved.user",
                Password = "hashed",
                Email = "approved@example.com"
            });
        context.RescuerApplications.AddRange(
            new RescuerApplication
            {
                Id = 1,
                UserId = pendingUserId,
                Status = "Pending",
                SubmittedAt = DateTime.UtcNow.AddDays(-1)
            },
            new RescuerApplication
            {
                Id = 2,
                UserId = approvedUserId,
                Status = "Approved",
                SubmittedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        var repository = new RescuerApplicationRepository(new UnitOfWork(context, NullLogger<UnitOfWork>.Instance));

        var result = await repository.GetPagedAsync(
            pageNumber: 1,
            pageSize: 10,
            status: RescuerApplicationStatus.Approved);

        var item = Assert.Single(result.Items);
        Assert.Equal(2, item.Id);
        Assert.Equal("Approved", item.Status);
        Assert.Equal(1, result.TotalCount);
    }
}
