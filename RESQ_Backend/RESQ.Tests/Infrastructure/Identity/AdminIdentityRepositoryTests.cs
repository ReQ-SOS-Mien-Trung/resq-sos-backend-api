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
    public async Task GetPagedForPermissionAsync_IncludesUsersWithDirectPermissions_AndExcludesBannedOrIneligible()
    {
        var options = new DbContextOptionsBuilder<ResQDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ResQDbContext(options);
        var directPermissionUserId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var eligibleRescuerUserId = Guid.Parse("30000000-0000-0000-0000-000000000002");
        var bannedUserId = Guid.Parse("30000000-0000-0000-0000-000000000003");
        var ineligibleRescuerUserId = Guid.Parse("30000000-0000-0000-0000-000000000004");

        context.Permissions.Add(new Permission
        {
            Id = 1,
            Code = "system.user.view",
            Name = "System User View"
        });

        context.Users.AddRange(
            new User
            {
                Id = directPermissionUserId,
                RoleId = 2,
                FirstName = "Direct",
                LastName = "Permission",
                Username = "direct.permission",
                Password = "hashed",
                Email = "direct@example.com",
                CreatedAt = new DateTime(2026, 4, 17, 8, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = eligibleRescuerUserId,
                RoleId = 3,
                FirstName = "Eligible",
                LastName = "Rescuer",
                Username = "eligible.rescuer",
                Password = "hashed",
                Email = "eligible@example.com",
                CreatedAt = new DateTime(2026, 4, 18, 8, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = bannedUserId,
                RoleId = 2,
                FirstName = "Banned",
                LastName = "User",
                Username = "banned.user",
                Password = "hashed",
                Email = "banned@example.com",
                IsBanned = true,
                CreatedAt = new DateTime(2026, 4, 19, 8, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = ineligibleRescuerUserId,
                RoleId = 3,
                FirstName = "Ineligible",
                LastName = "Rescuer",
                Username = "ineligible.rescuer",
                Password = "hashed",
                Email = "ineligible@example.com",
                CreatedAt = new DateTime(2026, 4, 20, 8, 0, 0, DateTimeKind.Utc)
            });

        context.RescuerProfiles.AddRange(
            new RescuerProfile
            {
                UserId = eligibleRescuerUserId,
                RescuerType = "Core",
                IsEligibleRescuer = true
            },
            new RescuerProfile
            {
                UserId = ineligibleRescuerUserId,
                RescuerType = "Volunteer",
                IsEligibleRescuer = false
            });

        context.UserPermissions.Add(new UserPermission
        {
            UserId = directPermissionUserId,
            ClaimId = 1,
            IsGranted = true
        });

        await context.SaveChangesAsync();

        var repository = new UserRepository(new UnitOfWork(context, NullLogger<UnitOfWork>.Instance));

        var result = await repository.GetPagedForPermissionAsync(pageNumber: 1, pageSize: 10);

        Assert.Equal(2, result.TotalCount);
        Assert.Contains(result.Items, item => item.Id == directPermissionUserId);
        Assert.Contains(result.Items, item => item.Id == eligibleRescuerUserId);
        Assert.DoesNotContain(result.Items, item => item.Id == bannedUserId);
        Assert.DoesNotContain(result.Items, item => item.Id == ineligibleRescuerUserId);
    }

    [Fact]
    public async Task GetPagedForPermissionAsync_FiltersByRoleNamePhoneEmail_AndAppliesPaginationOrdering()
    {
        var options = new DbContextOptionsBuilder<ResQDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ResQDbContext(options);
        var lanNguyenId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        var minhTranId = Guid.Parse("40000000-0000-0000-0000-000000000002");
        var lanPhamId = Guid.Parse("40000000-0000-0000-0000-000000000003");

        context.Users.AddRange(
            new User
            {
                Id = lanNguyenId,
                RoleId = 4,
                FirstName = "Lan",
                LastName = "Nguyen",
                Username = "lan.nguyen",
                Password = "hashed",
                Phone = "0901234567",
                Email = "lan@example.com",
                CreatedAt = new DateTime(2026, 4, 17, 8, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = minhTranId,
                RoleId = 4,
                FirstName = "Minh",
                LastName = "Tran",
                Username = "minh.tran",
                Password = "hashed",
                Phone = "0912345678",
                Email = "minh@example.com",
                CreatedAt = new DateTime(2026, 4, 18, 8, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = lanPhamId,
                RoleId = 2,
                FirstName = "Lan",
                LastName = "Pham",
                Username = "lan.pham",
                Password = "hashed",
                Phone = "0907777777",
                Email = "lan.pham@example.com",
                CreatedAt = new DateTime(2026, 4, 19, 8, 0, 0, DateTimeKind.Utc)
            });

        await context.SaveChangesAsync();

        var repository = new UserRepository(new UnitOfWork(context, NullLogger<UnitOfWork>.Instance));

        var roleFiltered = await repository.GetPagedForPermissionAsync(pageNumber: 1, pageSize: 10, roleId: 4);
        Assert.Equal(2, roleFiltered.TotalCount);
        Assert.All(roleFiltered.Items, item => Assert.Equal(4, item.RoleId));

        var nameFiltered = await repository.GetPagedForPermissionAsync(pageNumber: 1, pageSize: 10, name: "nguyen lan");
        var nameItem = Assert.Single(nameFiltered.Items);
        Assert.Equal(lanNguyenId, nameItem.Id);

        var phoneFiltered = await repository.GetPagedForPermissionAsync(pageNumber: 1, pageSize: 10, phone: "090");
        Assert.Equal(2, phoneFiltered.TotalCount);
        Assert.Contains(phoneFiltered.Items, item => item.Id == lanNguyenId);
        Assert.Contains(phoneFiltered.Items, item => item.Id == lanPhamId);

        var emailFiltered = await repository.GetPagedForPermissionAsync(pageNumber: 1, pageSize: 10, email: "MINH@EXAMPLE");
        var emailItem = Assert.Single(emailFiltered.Items);
        Assert.Equal(minhTranId, emailItem.Id);

        var paged = await repository.GetPagedForPermissionAsync(pageNumber: 2, pageSize: 1);
        var pagedItem = Assert.Single(paged.Items);
        Assert.Equal(minhTranId, pagedItem.Id);
        Assert.Equal(3, paged.TotalCount);
        Assert.Equal(2, paged.PageNumber);
        Assert.Equal(1, paged.PageSize);
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
