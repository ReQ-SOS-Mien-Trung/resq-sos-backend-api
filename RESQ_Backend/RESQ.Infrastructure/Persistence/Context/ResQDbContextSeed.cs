using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Persistence.Seeding;

namespace RESQ.Infrastructure.Persistence.Context;

public partial class ResQDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.SeedStaticModelData();
    }
}
