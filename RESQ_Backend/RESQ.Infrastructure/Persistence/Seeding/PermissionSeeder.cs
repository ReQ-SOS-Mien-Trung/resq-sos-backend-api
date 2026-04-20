using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Constants;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class PermissionSeeder
{
    private sealed record PermissionSeedDefinition(int Id, string Code, string Name, string Description);

    private static readonly PermissionSeedDefinition[] PermissionDefinitions =
    [
        new(1, PermissionConstants.SystemConfigManage, "Quản lý Cấu hình Hệ thống", "Tạo/sửa/xóa Role, gán quyền động"),
        new(2, PermissionConstants.SystemUserManage, "Quản lý Người dùng", "Ban/unban, tạo/sửa tài khoản, đổi role"),
        new(3, PermissionConstants.SystemUserView, "Xem Danh sách User & Role", "Chỉ được xem, không sửa"),
        new(4, PermissionConstants.InventoryGlobalManage, "Quản lý Kho Tổng", "Xuất/nhập/tồn, điều chuyển giữa các kho"),
        new(5, PermissionConstants.InventoryGlobalView, "Xem Tổng quan Tồn kho", "Xem inventory toàn bộ kho để ra quyết định"),
        // ID 6 is skipped or you can remove completely: new(6, PermissionConstants.InventoryGlobalManage, "Quản lý Kho Nhánh", "Xuất/nhập/kiểm kê kho được giao, duyệt yêu cầu vật phẩm"),
        new(7, PermissionConstants.InventoryDepotPointView, "Xem Tồn kho Điểm Tập kết", "Xem tồn kho tại điểm tập kết của mình"),
        new(8, PermissionConstants.InventorySupplyRequestCreate, "Tạo Phiếu Yêu cầu vật phẩm", "Tạo phiếu yêu cầu cấp phát vật phẩm cho đội"),
        new(9, PermissionConstants.PersonnelDepotBranchManage, "Quản lý Thủ kho Nhánh", "Quản lý danh sách thủ kho nhánh"),
        new(10, PermissionConstants.PersonnelGlobalManage, "Quản lý Nhân sự Toàn cục", "Điều phối nhân sự, tạo Team, chỉ định Core/Volunteer"),
        new(11, PermissionConstants.PersonnelPointManage, "Quản lý Nhân sự Điểm", "Tạo Team và phân bổ lực lượng nội bộ điểm tập kết"),
        new(12, PermissionConstants.PersonnelTeamView, "Xem Thành viên Team", "Xem danh sách thành viên trong Team"),
        new(13, PermissionConstants.PersonnelStatusReport, "Báo cáo Trạng thái Cá nhân", "Báo cáo trạng thái sẵn sàng của cá nhân"),
        new(14, PermissionConstants.MissionGlobalManage, "Quản lý Chiến dịch Toàn cục", "Nhận yêu cầu cứu hộ, tạo và duyệt Mission tổng"),
        new(15, PermissionConstants.MissionPointManage, "Quản lý Chiến dịch Điểm", "Tạo Mission cấp cơ sở, giao Mission cho Team"),
        new(16, PermissionConstants.MissionTeamUpdate, "Cập nhật Trạng thái Mission", "Nhận Mission, cập nhật trạng thái tổng của Mission"),
        new(17, PermissionConstants.MissionView, "Xem Thông tin Mission", "Xem thông tin, bối cảnh Mission của đội"),
        new(18, PermissionConstants.ActivityGlobalView, "Xem Tiến độ Chung", "Theo dõi tiến độ chung toàn hệ thống"),
        new(19, PermissionConstants.ActivityPointView, "Xem Tiến độ Điểm", "Theo dõi tiến độ đội nhà tại điểm"),
        new(20, PermissionConstants.ActivityTeamManage, "Quản lý Hoạt động Team", "Tạo Activity, assign cho Volunteer, duyệt kết quả"),
        new(21, PermissionConstants.ActivityOwnManage, "Quản lý Hoạt động Cá nhân", "Nhận Activity được assign, báo cáo, cập nhật trạng thái"),
        new(22, PermissionConstants.SosRequestCreate, "Tạo Yêu cầu Cứu hộ", "Gửi yêu cầu cứu hộ khẩn cấp"),
        new(23, PermissionConstants.SosRequestView, "Xem Yêu cầu Cứu hộ", "Xem danh sách và chi tiết yêu cầu cứu hộ"),
        new(24, PermissionConstants.IdentitySelfView, "Xem Thông tin Cá nhân", "Xem hồ sơ, thông tin user hiện tại và quyền hiệu lực của chính mình"),
        new(25, PermissionConstants.IdentityProfileUpdate, "Cập nhật Hồ sơ Cá nhân", "Cập nhật hồ sơ cá nhân của chính mình"),
        new(26, PermissionConstants.IdentityNotificationDeviceManage, "Quản lý Thiết bị Thông báo", "Đăng ký và gỡ FCM token của chính mình"),
        new(27, PermissionConstants.IdentityRelativeProfileView, "Xem Hồ sơ Người thân", "Xem danh sách hồ sơ người thân của chính mình"),
        new(28, PermissionConstants.IdentityRelativeProfileManage, "Quản lý Hồ sơ Người thân", "Tạo/sửa/xóa/đồng bộ hồ sơ người thân của chính mình"),
        new(29, PermissionConstants.IdentitySessionManage, "Quản lý Phiên Đăng nhập", "Làm mới token và đăng xuất các phiên của chính mình"),
        new(30, PermissionConstants.NotificationSelfView, "Xem Thông báo Cá nhân", "Xem danh sách thông báo của chính mình"),
        new(31, PermissionConstants.NotificationSelfManage, "Quản lý Thông báo Cá nhân", "Đánh dấu đã đọc và quản lý trạng thái thông báo cá nhân"),
        new(32, PermissionConstants.ConversationSelfView, "Xem Hội thoại Cá nhân", "Xem conversation và lịch sử chat mà mình là participant"),
        new(33, PermissionConstants.ConversationSelfManage, "Thao tác Hội thoại Cá nhân", "Gửi tin nhắn và thao tác trong conversation mà mình là participant"),
        new(34, PermissionConstants.ConversationCoordinatorManage, "Điều phối Hội thoại Hỗ trợ", "Nhận danh sách chờ và tham gia/rời conversation hỗ trợ"),
        new(35, PermissionConstants.PersonnelTeamSelfView, "Xem Đội của Tôi", "Xem thông tin đội hiện tại của chính mình"),
        new(36, PermissionConstants.PersonnelTeamAvailabilityManage, "Quản lý Sẵn sàng của Đội", "Đánh dấu đội sẵn sàng hoặc không sẵn sàng"),
        new(37, PermissionConstants.PersonnelAssemblyPointView, "Xem Điểm Tập kết", "Xem danh sách điểm tập kết phục vụ mobile rescuer"),
        new(38, PermissionConstants.PersonnelAssemblyEventSelfView, "Xem Sự kiện Tập trung của Tôi", "Xem sự kiện tập trung mà mình tham gia"),
        new(39, PermissionConstants.PersonnelAssemblyEventCheckIn, "Check-in Sự kiện Tập trung", "Check-in vào sự kiện tập trung của chính mình"),
        new(40, PermissionConstants.MissionSelfView, "Xem Mission của Đội", "Xem danh sách mission và context của đội hiện tại"),
        new(41, PermissionConstants.ActivitySelfView, "Xem Activity của Đội", "Xem activity được giao cho đội hiện tại"),
        new(42, PermissionConstants.MissionExecutionComplete, "Hoàn tất Thực thi Mission Team", "Xác nhận đội đã hoàn tất phần thực thi ngoài hiện trường"),
        new(43, PermissionConstants.MissionReportView, "Xem Báo cáo Mission Team", "Xem báo cáo hoặc draft hiện tại của mission team"),
        new(44, PermissionConstants.MissionReportEdit, "Chỉnh sửa Báo cáo Mission Team", "Lưu draft và cập nhật nội dung báo cáo mission team"),
        new(45, PermissionConstants.MissionReportSubmit, "Nộp Báo cáo Mission Team", "Nộp báo cáo cuối cùng của mission team"),
        new(46, PermissionConstants.MissionIncidentReport, "Báo Mission Incident", "Báo sự cố ở mission hoặc activity"),
        new(47, PermissionConstants.MissionIncidentView, "Xem Mission Incident", "Xem danh sách và chi tiết incident của mission hoặc team"),
        new(48, PermissionConstants.MissionIncidentManage, "Quản lý Mission Incident", "Cập nhật trạng thái incident và xử lý điều phối liên quan"),
        new(49, PermissionConstants.SosRequestCancelOwn, "Huỷ Yêu cầu Cứu hộ của Tôi", "Truy cập endpoint huỷ SOS; domain vẫn kiểm tra owner hoặc companion cụ thể")
    ];

    private static readonly IReadOnlyDictionary<string, int> PermissionIdsByCode = PermissionDefinitions
        .ToDictionary(permission => permission.Code, permission => permission.Id, StringComparer.Ordinal);

    public static void SeedPermission(this ModelBuilder modelBuilder)
    {
        SeedPermissions(modelBuilder);
        SeedRolePermissions(modelBuilder);
    }

    public static IReadOnlyList<Permission> CreatePermissions()
    {
        return PermissionDefinitions
            .Select(permission => new Permission
            {
                Id = permission.Id,
                Code = permission.Code,
                Name = permission.Name,
                Description = permission.Description
            })
            .ToList();
    }

    public static IReadOnlyList<RolePermission> CreateRolePermissions()
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

        return rolePermissions;
    }

    private static void SeedPermissions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Permission>().HasData(CreatePermissions());
    }

    private static void SeedRolePermissions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RolePermission>().HasData(CreateRolePermissions());
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
