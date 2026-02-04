using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class IdentitySeeder
{
    public static void SeedIdentity(this ModelBuilder modelBuilder)
    {
        SeedRoles(modelBuilder);
        SeedUsers(modelBuilder);
    }

    private static void SeedRoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Admin" },
            new Role { Id = 2, Name = "Coordinator" },
            new Role { Id = 3, Name = "Rescuer" },
            new Role { Id = 4, Name = "Manager" },
            new Role { Id = 5, Name = "Victim" }
        );
    }

    private static void SeedUsers(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = SeedConstants.AdminUserId,
                RoleId = 1,
                FullName = "Nguyễn Văn Admin",
                Username = "admin",
                Phone = "0901234567",
                Password = SeedConstants.AdminPasswordHash,
                IsOnboarded = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.CoordinatorUserId,
                RoleId = 2,
                FullName = "Trần Thị Coordinator",
                Username = "coordinator",
                Phone = "0912345678",
                Password = SeedConstants.CoordinatorPasswordHash,
                IsOnboarded = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.RescuerUserId,
                RoleId = 3,
                FullName = "Lê Văn Rescuer",
                Username = "rescuer",
                Phone = "0923456789",
                Password = SeedConstants.RescuerPasswordHash,
                IsOnboarded = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.ManagerUserId,
                RoleId = 4,
                FullName = "Phạm Thị Manager",
                Username = "manager",
                Phone = "0934567890",
                Password = SeedConstants.ManagerPasswordHash,
                IsOnboarded = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = SeedConstants.VictimUserId,
                RoleId = 5,
                FullName = "Hoàng Văn Victim",
                Username = "victim",
                Phone = "0945678901",
                Password = SeedConstants.VictimPasswordHash,
                IsOnboarded = true,
                CreatedAt = now,
                UpdatedAt = now
            }
        );
    }
}
