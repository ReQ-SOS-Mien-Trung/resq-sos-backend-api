using Microsoft.EntityFrameworkCore;
using RESQ.Infrastructure.Persistence.Seeding;

namespace RESQ.Infrastructure.Persistence.Context;

public partial class ResQDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // 1. Identity (Users, Roles)
        modelBuilder.SeedIdentity();

        // 2. Personnel (Abilities, Teams, Assembly Points)
        modelBuilder.SeedPersonnel();

        // 3. Logistics (Depots, Inventory, Organizations)
        modelBuilder.SeedLogistics();

        // 4. Emergency (SOS Requests, Clusters)
        modelBuilder.SeedEmergency();

        // 5. Operations (Missions, Chats)
        modelBuilder.SeedOperations();

        // 6. System (Notifications, Prompts)
        modelBuilder.SeedSystem();

        // 7. AI Analysis (Suggestions)
        modelBuilder.SeedAiAnalysis();
    }
}
