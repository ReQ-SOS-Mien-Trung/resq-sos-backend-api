using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Domain.Enum.Identity;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Persistence.Base;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Personnel;

namespace RESQ.Tests.Infrastructure.Personnel;

public class PersonnelQueryRepositoryTests
{
    [Fact]
    public async Task GetRescuersAsync_SearchesVietnameseGivenName()
    {
        await using var context = CreateContext();
        var rescuerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        SeedEligibleRescuer(context, rescuerId, firstName: "Anh Tuấn", lastName: "Nguyễn", username: "rescue-alpha");
        await context.SaveChangesAsync();

        var repository = CreateRepository(context);

        var result = await repository.GetRescuersAsync(pageNumber: 1, pageSize: 20, search: "Tuấn");

        var rescuer = Assert.Single(result.Items);
        Assert.Equal(rescuerId, rescuer.Id);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetRescuersAsync_SearchesUnaccentedFullName()
    {
        await using var context = CreateContext();
        var rescuerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        SeedEligibleRescuer(context, rescuerId, firstName: "Anh Tuấn", lastName: "Nguyễn", username: "rescue-beta");
        SeedEligibleRescuer(
            context,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            firstName: "Minh",
            lastName: "Tran",
            username: "rescue-gamma");
        await context.SaveChangesAsync();

        var repository = CreateRepository(context);

        var result = await repository.GetRescuersAsync(pageNumber: 1, pageSize: 20, search: "Nguyen Anh Tuan");

        var rescuer = Assert.Single(result.Items);
        Assert.Equal(rescuerId, rescuer.Id);
        Assert.Equal(1, result.TotalCount);
    }

    private static ResQDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ResQDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ResQDbContext(options);
    }

    private static PersonnelQueryRepository CreateRepository(ResQDbContext context)
    {
        var unitOfWork = new UnitOfWork(context, NullLogger<UnitOfWork>.Instance);
        return new PersonnelQueryRepository(unitOfWork);
    }

    private static void SeedEligibleRescuer(
        ResQDbContext context,
        Guid userId,
        string firstName,
        string lastName,
        string username)
    {
        var user = new User
        {
            Id = userId,
            RoleId = 3,
            FirstName = firstName,
            LastName = lastName,
            Username = username,
            Phone = "0900000000",
            Password = "hashed-password",
            Email = $"{username}@example.com",
            CreatedAt = DateTime.UtcNow
        };

        var profile = new RescuerProfile
        {
            UserId = userId,
            User = user,
            RescuerType = RescuerType.Core.ToString(),
            IsEligibleRescuer = true,
            Step = 3
        };

        context.Users.Add(user);
        context.RescuerProfiles.Add(profile);
    }
}
