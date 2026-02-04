namespace RESQ.Infrastructure.Persistence.Seeding;

public static class SeedConstants
{
    // Passwords
    public const string AdminPasswordHash = "$2a$11$ijKjJLUF47vj/JkLAg5C4eOzZ11yQ1ORWquJTBlIOPIIeWTimQCBm"; // Admin@123
    public const string CoordinatorPasswordHash = "$2a$11$tawo9jpZGHHA25NfCF6hUOLYIcgETiaCTvmsM4oOd0VH5mwMkn6.O"; // Coordinator@123
    public const string RescuerPasswordHash = "$2a$11$RipGftiyzl4tYLZZdLLJ4ufKnogeR8kWp1DeKlpj44eQcWlzNk3.u"; // Rescuer@123
    public const string ManagerPasswordHash = "$2a$11$mIi0t6MBeHaLRz8X/EUAvOn0RsbZs4pnJ4weyoVkusnCf2grE45oG"; // Manager@123
    public const string VictimPasswordHash = "$2a$11$on1XCfJiZ.y.280Rx2rKkOFOPn2UnX42ay7V8pZ2QJUkDW4IbD38O"; // Victim@123

    // User GUIDs
    public static readonly Guid AdminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid CoordinatorUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid RescuerUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid ManagerUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid VictimUserId = Guid.Parse("55555555-5555-5555-5555-555555555555");
}