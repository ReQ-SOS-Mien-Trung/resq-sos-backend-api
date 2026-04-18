namespace RESQ.Infrastructure.Persistence.Seeding;

public static class SeedConstants
{
    // Passwords
    public const string AdminPasswordHash = "$2a$11$ijKjJLUF47vj/JkLAg5C4eOzZ11yQ1ORWquJTBlIOPIIeWTimQCBm"; // Admin@123
    public const string CoordinatorPasswordHash = "$2a$11$tawo9jpZGHHA25NfCF6hUOLYIcgETiaCTvmsM4oOd0VH5mwMkn6.O"; // Coordinator@123
    public const string RescuerPasswordHash = "$2a$11$RipGftiyzl4tYLZZdLLJ4ufKnogeR8kWp1DeKlpj44eQcWlzNk3.u"; // Rescuer@123
    public const string ManagerPasswordHash = "$2a$11$mIi0t6MBeHaLRz8X/EUAvOn0RsbZs4pnJ4weyoVkusnCf2grE45oG"; // Manager@123
    public const string VictimPasswordHash = "$2a$11$on1XCfJiZ.y.280Rx2rKkOFOPn2UnX42ay7V8pZ2QJUkDW4IbD38O"; // Victim@123
    public const string DemoVictimPinPasswordHash = "$2a$11$ZzbWM8IJCOXubz5XuI/g8euSE0/zM7islhebwX.SiGK/ilJx3ieEy"; // 142200

    // User GUIDs
    public static readonly Guid AdminUserId       = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid CoordinatorUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid RescuerUserId     = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid ManagerUserId     = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid Manager2UserId    = Guid.Parse("44444444-4444-4444-4444-444444444442");
    public static readonly Guid Manager3UserId    = Guid.Parse("44444444-4444-4444-4444-444444444443");
    public static readonly Guid Manager4UserId    = Guid.Parse("44444444-4444-4444-4444-444444444445");
    public static readonly Guid Manager5UserId    = Guid.Parse("44444444-4444-4444-4444-444444444446");
    public static readonly Guid Manager6UserId    = Guid.Parse("44444444-4444-4444-4444-444444444447");
    public static readonly Guid Manager7UserId    = Guid.Parse("44444444-4444-4444-4444-444444444448");
    public static readonly Guid VictimUserId      = Guid.Parse("55555555-5555-5555-5555-555555555555");

    // Rescuer Applicant GUIDs (dùng để seed đơn đăng ký rescuer)
    public static readonly Guid Applicant1UserId = Guid.Parse("66666666-6666-6666-6666-666666666661");
    public static readonly Guid Applicant2UserId = Guid.Parse("66666666-6666-6666-6666-666666666662");
    public static readonly Guid Applicant3UserId = Guid.Parse("66666666-6666-6666-6666-666666666663");
    public static readonly Guid Applicant4UserId = Guid.Parse("66666666-6666-6666-6666-666666666664");
    public static readonly Guid Applicant5UserId = Guid.Parse("66666666-6666-6666-6666-666666666665");

    // Password hash cho applicant (dùng chung Victim@123)
    public const string ApplicantPasswordHash = VictimPasswordHash;
}
