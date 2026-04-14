using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Constants;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class PermissionSeeder
{
    private sealed record PermissionSeedDefinition(int Id, string Code, string Name, string Description);

    private static readonly PermissionSeedDefinition[] PermissionDefinitions =
    [
        new(1, PermissionConstants.SystemConfigManage, "Qu?n lý C?u hình H? th?ng", "T?o/s?a/xóa Role, gán quy?n d?ng"),
        new(2, PermissionConstants.SystemUserManage, "Qu?n lý Ngu?i dùng", "Ban/unban, t?o/s?a tài kho?n, d?i role"),
        new(3, PermissionConstants.SystemUserView, "Xem Danh sách User & Role", "Ch? du?c xem, không s?a"),
        new(4, PermissionConstants.InventoryGlobalManage, "Qu?n lý Kho T?ng", "Xu?t/nh?p/t?n, di?u chuy?n gi?a các kho"),
        new(5, PermissionConstants.InventoryGlobalView, "Xem T?ng quan T?n kho", "Xem inventory toàn b? kho d? ra quy?t d?nh"),
        // ID 6 is skipped or you can remove completely: new(6, PermissionConstants.InventoryGlobalManage, "Qu?n lý Kho Nhánh", "Xu?t/nh?p/ki?m kê kho du?c giao, duy?t yêu c?u v?t ph?m"),
        new(7, PermissionConstants.InventoryDepotPointView, "Xem T?n kho Ði?m T?p k?t", "Xem t?n kho t?i di?m t?p k?t c?a mình"),
        new(8, PermissionConstants.InventorySupplyRequestCreate, "T?o Phi?u Yêu c?u v?t ph?m", "T?o phi?u yêu c?u c?p phát v?t ph?m cho d?i"),
        new(9, PermissionConstants.PersonnelDepotBranchManage, "Qu?n lý Th? kho Nhánh", "Qu?n lý danh sách th? kho nhánh"),
        new(10, PermissionConstants.PersonnelGlobalManage, "Qu?n lý Nhân s? Toàn c?c", "Ði?u ph?i nhân s?, t?o Team, ch? d?nh Core/Volunteer"),
        new(11, PermissionConstants.PersonnelPointManage, "Qu?n lý Nhân s? Ði?m", "T?o Team và phân b? l?c lu?ng n?i b? di?m t?p k?t"),
        new(12, PermissionConstants.PersonnelTeamView, "Xem Thành viên Team", "Xem danh sách thành viên trong Team"),
        new(13, PermissionConstants.PersonnelStatusReport, "Báo cáo Tr?ng thái Cá nhân", "Báo cáo tr?ng thái s?n sàng c?a cá nhân"),
        new(14, PermissionConstants.MissionGlobalManage, "Qu?n lý Chi?n d?ch Toàn c?c", "Nh?n yêu c?u c?u h?, t?o và duy?t Mission t?ng"),
        new(15, PermissionConstants.MissionPointManage, "Qu?n lý Chi?n d?ch Ði?m", "T?o Mission c?p co s?, giao Mission cho Team"),
        new(16, PermissionConstants.MissionTeamUpdate, "C?p nh?t Tr?ng thái Mission", "Nh?n Mission, c?p nh?t tr?ng thái t?ng c?a Mission"),
        new(17, PermissionConstants.MissionView, "Xem Thông tin Mission", "Xem thông tin, b?i c?nh Mission c?a d?i"),
        new(18, PermissionConstants.ActivityGlobalView, "Xem Ti?n d? Chung", "Theo dõi ti?n d? chung toàn h? th?ng"),
        new(19, PermissionConstants.ActivityPointView, "Xem Ti?n d? Ði?m", "Theo dõi ti?n d? d?i nhà t?i di?m"),
        new(20, PermissionConstants.ActivityTeamManage, "Qu?n lý Ho?t d?ng Team", "T?o Activity, assign cho Volunteer, duy?t k?t qu?"),
        new(21, PermissionConstants.ActivityOwnManage, "Qu?n lý Ho?t d?ng Cá nhân", "Nh?n Activity du?c assign, báo cáo, c?p nh?t tr?ng thái"),
        new(22, PermissionConstants.SosRequestCreate, "T?o Yêu c?u C?u h?", "G?i yêu c?u c?u h? kh?n c?p"),
        new(23, PermissionConstants.SosRequestView, "Xem Yêu c?u C?u h?", "Xem danh sách và chi ti?t yêu c?u c?u h?"),
        new(24, PermissionConstants.IdentitySelfView, "Xem Thông tin Cá nhân", "Xem h? so, thông tin user hi?n t?i và quy?n hi?u l?c c?a chính mình"),
        new(25, PermissionConstants.IdentityProfileUpdate, "C?p nh?t H? so Cá nhân", "C?p nh?t h? so cá nhân c?a chính mình"),
        new(26, PermissionConstants.IdentityNotificationDeviceManage, "Qu?n lý Thi?t b? Thông báo", "Ðang ký và g? FCM token c?a chính mình"),
        new(27, PermissionConstants.IdentityRelativeProfileView, "Xem H? so Ngu?i thân", "Xem danh sách h? so ngu?i thân c?a chính mình"),
        new(28, PermissionConstants.IdentityRelativeProfileManage, "Qu?n lý H? so Ngu?i thân", "T?o/s?a/xóa/d?ng b? h? so ngu?i thân c?a chính mình"),
        new(29, PermissionConstants.IdentitySessionManage, "Qu?n lý Phiên Ðang nh?p", "Làm m?i token và dang xu?t các phiên c?a chính mình"),
        new(30, PermissionConstants.NotificationSelfView, "Xem Thông báo Cá nhân", "Xem danh sách thông báo c?a chính mình"),
        new(31, PermissionConstants.NotificationSelfManage, "Qu?n lý Thông báo Cá nhân", "Ðánh d?u dã d?c và qu?n lý tr?ng thái thông báo cá nhân"),
        new(32, PermissionConstants.ConversationSelfView, "Xem H?i tho?i Cá nhân", "Xem conversation và l?ch s? chat mà mình là participant"),
        new(33, PermissionConstants.ConversationSelfManage, "Thao tác H?i tho?i Cá nhân", "G?i tin nh?n và thao tác trong conversation mà mình là participant"),
        new(34, PermissionConstants.ConversationCoordinatorManage, "Ði?u ph?i H?i tho?i H? tr?", "Nh?n danh sách ch? và tham gia/r?i conversation h? tr?"),
        new(35, PermissionConstants.PersonnelTeamSelfView, "Xem Ð?i c?a Tôi", "Xem thông tin d?i hi?n t?i c?a chính mình"),
        new(36, PermissionConstants.PersonnelTeamAvailabilityManage, "Qu?n lý S?n sàng c?a Ð?i", "Ðánh d?u d?i s?n sàng ho?c không s?n sàng"),
        new(37, PermissionConstants.PersonnelAssemblyPointView, "Xem Ði?m T?p k?t", "Xem danh sách di?m t?p k?t ph?c v? mobile rescuer"),
        new(38, PermissionConstants.PersonnelAssemblyEventSelfView, "Xem S? ki?n T?p trung c?a Tôi", "Xem s? ki?n t?p trung mà mình tham gia"),
        new(39, PermissionConstants.PersonnelAssemblyEventCheckIn, "Check-in S? ki?n T?p trung", "Check-in vào s? ki?n t?p trung c?a chính mình"),
        new(40, PermissionConstants.MissionSelfView, "Xem Mission c?a Ð?i", "Xem danh sách mission và context c?a d?i hi?n t?i"),
        new(41, PermissionConstants.ActivitySelfView, "Xem Activity c?a Ð?i", "Xem activity du?c giao cho d?i hi?n t?i"),
        new(42, PermissionConstants.MissionExecutionComplete, "Hoàn t?t Th?c thi Mission Team", "Xác nh?n d?i dã hoàn t?t ph?n th?c thi ngoài hi?n tru?ng"),
        new(43, PermissionConstants.MissionReportView, "Xem Báo cáo Mission Team", "Xem báo cáo ho?c draft hi?n t?i c?a mission team"),
        new(44, PermissionConstants.MissionReportEdit, "Ch?nh s?a Báo cáo Mission Team", "Luu draft và c?p nh?t n?i dung báo cáo mission team"),
        new(45, PermissionConstants.MissionReportSubmit, "N?p Báo cáo Mission Team", "N?p báo cáo cu?i cùng c?a mission team"),
        new(46, PermissionConstants.MissionIncidentReport, "Báo Mission Incident", "Báo s? c? ? mission ho?c activity"),
        new(47, PermissionConstants.MissionIncidentView, "Xem Mission Incident", "Xem danh sách và chi ti?t incident c?a mission ho?c team"),
        new(48, PermissionConstants.MissionIncidentManage, "Qu?n lý Mission Incident", "C?p nh?t tr?ng thái incident và x? lý di?u ph?i liên quan"),
        new(49, PermissionConstants.SosRequestCancelOwn, "Hu? Yêu c?u C?u h? c?a Tôi", "Truy c?p endpoint hu? SOS; domain v?n ki?m tra owner ho?c companion c? th?")
    ];

    private static readonly IReadOnlyDictionary<string, int> PermissionIdsByCode = PermissionDefinitions
        .ToDictionary(permission => permission.Code, permission => permission.Id, StringComparer.Ordinal);

    public static void SeedPermission(this ModelBuilder modelBuilder)
    {
        SeedPermissions(modelBuilder);
        SeedRolePermissions(modelBuilder);
    }

    private static void SeedPermissions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Permission>().HasData(
            PermissionDefinitions.Select(permission => new Permission
            {
                Id = permission.Id,
                Code = permission.Code,
                Name = permission.Name,
                Description = permission.Description
            }));
    }

    private static void SeedRolePermissions(ModelBuilder modelBuilder)
    {
        var rolePermissions = new List<RolePermission>();

        GrantAll(rolePermissions, RoleConstants.Admin);

        Grant(rolePermissions, RoleConstants.Coordinator,
            PermissionConstants.SystemUserView,
            PermissionConstants.IdentitySelfView,
            PermissionConstants.IdentityProfileUpdate,
            PermissionConstants.IdentityNotificationDeviceManage,
            PermissionConstants.IdentityRelativeProfileView,
            PermissionConstants.IdentityRelativeProfileManage,
            PermissionConstants.IdentitySessionManage,
            PermissionConstants.NotificationSelfView,
            PermissionConstants.NotificationSelfManage,
            PermissionConstants.ConversationSelfView,
            PermissionConstants.ConversationSelfManage,
            PermissionConstants.ConversationCoordinatorManage,
            PermissionConstants.InventoryGlobalView,
            PermissionConstants.InventoryDepotPointView,
            PermissionConstants.PersonnelGlobalManage,
            PermissionConstants.PersonnelPointManage,
            PermissionConstants.PersonnelTeamSelfView,
            PermissionConstants.PersonnelTeamAvailabilityManage,
            PermissionConstants.PersonnelAssemblyPointView,
            PermissionConstants.PersonnelAssemblyEventSelfView,
            PermissionConstants.PersonnelAssemblyEventCheckIn,
            PermissionConstants.MissionGlobalManage,
            PermissionConstants.MissionPointManage,
            PermissionConstants.MissionSelfView,
            PermissionConstants.ActivityGlobalView,
            PermissionConstants.ActivityPointView,
            PermissionConstants.ActivitySelfView,
            PermissionConstants.MissionExecutionComplete,
            PermissionConstants.MissionReportView,
            PermissionConstants.MissionReportEdit,
            PermissionConstants.MissionReportSubmit,
            PermissionConstants.MissionIncidentReport,
            PermissionConstants.MissionIncidentView,
            PermissionConstants.MissionIncidentManage,
            PermissionConstants.SosRequestCreate,
            PermissionConstants.SosRequestView);

        Grant(rolePermissions, RoleConstants.Rescuer,
            PermissionConstants.IdentitySelfView,
            PermissionConstants.IdentityProfileUpdate,
            PermissionConstants.IdentityNotificationDeviceManage,
            PermissionConstants.IdentityRelativeProfileView,
            PermissionConstants.IdentityRelativeProfileManage,
            PermissionConstants.IdentitySessionManage,
            PermissionConstants.NotificationSelfView,
            PermissionConstants.NotificationSelfManage,
            PermissionConstants.InventorySupplyRequestCreate,
            PermissionConstants.PersonnelTeamView,
            PermissionConstants.PersonnelStatusReport,
            PermissionConstants.PersonnelTeamSelfView,
            PermissionConstants.PersonnelTeamAvailabilityManage,
            PermissionConstants.PersonnelAssemblyPointView,
            PermissionConstants.PersonnelAssemblyEventSelfView,
            PermissionConstants.PersonnelAssemblyEventCheckIn,
            PermissionConstants.MissionTeamUpdate,
            PermissionConstants.MissionView,
            PermissionConstants.MissionSelfView,
            PermissionConstants.ActivityTeamManage,
            PermissionConstants.ActivityOwnManage,
            PermissionConstants.ActivitySelfView,
            PermissionConstants.MissionExecutionComplete,
            PermissionConstants.MissionReportView,
            PermissionConstants.MissionReportEdit,
            PermissionConstants.MissionReportSubmit,
            PermissionConstants.MissionIncidentReport,
            PermissionConstants.MissionIncidentView,
            PermissionConstants.MissionIncidentManage);

        Grant(rolePermissions, RoleConstants.Manager,
            PermissionConstants.IdentitySelfView,
            PermissionConstants.IdentityProfileUpdate,
            PermissionConstants.IdentityNotificationDeviceManage,
            PermissionConstants.IdentityRelativeProfileView,
            PermissionConstants.IdentityRelativeProfileManage,
            PermissionConstants.IdentitySessionManage,
            PermissionConstants.NotificationSelfView,
            PermissionConstants.NotificationSelfManage,
            PermissionConstants.InventoryGlobalManage,
            PermissionConstants.InventoryGlobalView,
            PermissionConstants.InventoryGlobalManage,
            PermissionConstants.PersonnelDepotBranchManage);

        Grant(rolePermissions, RoleConstants.Victim,
            PermissionConstants.IdentitySelfView,
            PermissionConstants.IdentityProfileUpdate,
            PermissionConstants.IdentityNotificationDeviceManage,
            PermissionConstants.IdentityRelativeProfileView,
            PermissionConstants.IdentityRelativeProfileManage,
            PermissionConstants.IdentitySessionManage,
            PermissionConstants.NotificationSelfView,
            PermissionConstants.NotificationSelfManage,
            PermissionConstants.ConversationSelfView,
            PermissionConstants.ConversationSelfManage,
            PermissionConstants.SosRequestCreate,
            PermissionConstants.SosRequestCancelOwn);

        modelBuilder.Entity<RolePermission>().HasData(rolePermissions);
    }

    private static void GrantAll(ICollection<RolePermission> rolePermissions, int roleId)
    {
        foreach (var permission in PermissionDefinitions)
        {
            rolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                ClaimId = permission.Id,
                IsGranted = true
            });
        }
    }

    private static void Grant(ICollection<RolePermission> rolePermissions, int roleId, params string[] permissionCodes)
    {
        foreach (var permissionCode in permissionCodes.Distinct(StringComparer.Ordinal))
        {
            rolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                ClaimId = PermissionIdsByCode[permissionCode],
                IsGranted = true
            });
        }
    }
}
