using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Persistence.Context;

public partial class ResQDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Runtime demo data is seeded by DatabaseSeeder; keep migrations free of API test fixtures.
    }
}
