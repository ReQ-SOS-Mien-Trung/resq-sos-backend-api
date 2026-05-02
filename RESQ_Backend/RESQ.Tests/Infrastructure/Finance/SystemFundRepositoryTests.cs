using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Infrastructure.Entities.Finance;
using RESQ.Infrastructure.Persistence.Base;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Finance;

namespace RESQ.Tests.Infrastructure.Finance;

public class SystemFundRepositoryTests
{
    [Fact]
    public async Task UpdateAsync_WithTrackedSystemFund_UpdatesInPlace()
    {
        await using var context = CreateContext();
        context.SystemFunds.Add(new SystemFund
        {
            Id = 1,
            Name = "System fund",
            Balance = 1_000_000m,
            LastUpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var unitOfWork = new UnitOfWork(context, NullLogger<UnitOfWork>.Instance);
        var repository = new SystemFundRepository(unitOfWork);

        var fund = await repository.GetOrCreateAsync();
        fund.Debit(600_000m);

        await repository.UpdateAsync(fund);
        await unitOfWork.SaveAsync();

        var updated = await context.SystemFunds.SingleAsync(x => x.Id == 1);
        Assert.Equal(400_000m, updated.Balance);
    }

    private static ResQDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ResQDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ResQDbContext(options);
    }
}
