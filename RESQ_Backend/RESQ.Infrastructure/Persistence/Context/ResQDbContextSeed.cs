using Microsoft.EntityFrameworkCore;

namespace RESQ.Infrastructure.Persistence.Context;

public partial class ResQDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Demo data is seeded by DatabaseSeeder through EF's migration seeding hook.
    }
}
