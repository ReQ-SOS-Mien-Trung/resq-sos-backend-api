using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Constants;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Persistence.Context;

namespace RESQ.Infrastructure.Persistence.Seeding;

/// <summary>
/// Runtime seeder cho bảng <c>permissions</c> và <c>role_permissions</c>.
/// Được gọi tại startup (sau khi migrate). Idempotent – chỉ insert những record chưa tồn tại.
/// </summary>
public static class PermissionSeeder
{
    // ── Danh sách permission ──────────────────────────────────────────────
    private static readonly (string Code, string Name, string Description)[] PermissionDefs =
    [
        (PermissionConstants.SystemConfigManage,          "Quản lý Cấu hình Hệ thống",    "Tạo/sửa/xóa Role, gán quyền động"),
        (PermissionConstants.SystemUserManage,            "Quản lý Người dùng",            "Ban/unban, tạo/sửa tài khoản, đổi role"),
        (PermissionConstants.SystemUserView,              "Xem Danh sách User & Role",      "Chỉ được xem, không sửa"),
        (PermissionConstants.InventoryGlobalManage,       "Quản lý Kho Tổng",              "Xuất/nhập/tồn, điều chuyển giữa các kho"),
        (PermissionConstants.InventoryGlobalView,         "Xem Tổng quan Tồn kho",         "Xem inventory toàn bộ kho để ra quyết định"),
        (PermissionConstants.InventoryDepotManage,        "Quản lý Kho Nhánh",             "Xuất/nhập/kiểm kê kho được giao, duyệt yêu cầu vật tư"),
        (PermissionConstants.InventoryDepotPointView,     "Xem Tồn kho Điểm Tập kết",      "Xem tồn kho tại điểm tập kết của mình"),
        (PermissionConstants.InventorySupplyRequestCreate,"Tạo Phiếu Yêu cầu Vật tư",     "Tạo phiếu yêu cầu cấp phát vật tư cho đội"),
        (PermissionConstants.PersonnelDepotBranchManage,  "Quản lý Thủ kho Nhánh",         "Quản lý danh sách thủ kho nhánh"),
        (PermissionConstants.PersonnelGlobalManage,       "Quản lý Nhân sự Toàn cục",      "Điều phối nhân sự, tạo Team, chỉ định Core/Volunteer"),
        (PermissionConstants.PersonnelPointManage,        "Quản lý Nhân sự Điểm",          "Tạo Team và phân bổ lực lượng nội bộ điểm tập kết"),
        (PermissionConstants.PersonnelTeamView,           "Xem Thành viên Team",            "Xem danh sách thành viên trong Team"),
        (PermissionConstants.PersonnelStatusReport,       "Báo cáo Trạng thái Cá nhân",    "Báo cáo trạng thái sẵn sàng của cá nhân"),
        (PermissionConstants.MissionGlobalManage,         "Quản lý Chiến dịch Toàn cục",   "Nhận yêu cầu cứu hộ, tạo và duyệt Mission tổng"),
        (PermissionConstants.MissionPointManage,          "Quản lý Chiến dịch Điểm",       "Tạo Mission cấp cơ sở, giao Mission cho Team"),
        (PermissionConstants.MissionTeamUpdate,           "Cập nhật Trạng thái Mission",   "Nhận Mission, cập nhật trạng thái tổng của Mission"),
        (PermissionConstants.MissionView,                 "Xem Thông tin Mission",          "Xem thông tin, bối cảnh Mission của đội"),
        (PermissionConstants.ActivityGlobalView,          "Xem Tiến độ Chung",             "Theo dõi tiến độ chung toàn hệ thống"),
        (PermissionConstants.ActivityPointView,           "Xem Tiến độ Điểm",              "Theo dõi tiến độ đội nhà tại điểm"),
        (PermissionConstants.ActivityTeamManage,          "Quản lý Hoạt động Team",        "Tạo Activity, assign cho Volunteer, duyệt kết quả"),
        (PermissionConstants.ActivityOwnManage,           "Quản lý Hoạt động Cá nhân",     "Nhận Activity được assign, báo cáo, cập nhật trạng thái"),
        (PermissionConstants.SosRequestCreate,            "Tạo Yêu cầu Cứu hộ",           "Gửi yêu cầu cứu hộ khẩn cấp"),
        (PermissionConstants.SosRequestView,              "Xem Yêu cầu Cứu hộ",           "Xem danh sách và chi tiết yêu cầu cứu hộ"),
    ];

    // ── Role → danh sách permission codes ────────────────────────────────
    private static readonly Dictionary<int, string[]> RolePermissionMap = new()
    {
        // Admin: tất cả quyền
        [RoleConstants.Admin] = PermissionDefs.Select(p => p.Code).ToArray(),

        // Coordinator: xem user/role, xem kho tổng + điểm, quản lý nhân sự toàn cục + điểm,
        //              quản lý chiến dịch tổng + điểm, xem tiến độ chung + điểm, tạo/xem SOS
        [RoleConstants.Coordinator] =
        [
            PermissionConstants.SystemUserView,
            PermissionConstants.InventoryGlobalView,
            PermissionConstants.InventoryDepotPointView,
            PermissionConstants.PersonnelGlobalManage,
            PermissionConstants.PersonnelPointManage,
            PermissionConstants.MissionGlobalManage,
            PermissionConstants.MissionPointManage,
            PermissionConstants.ActivityGlobalView,
            PermissionConstants.ActivityPointView,
            PermissionConstants.SosRequestCreate,
            PermissionConstants.SosRequestView,
        ],

        // Rescuer: yêu cầu vật tư, xem team, báo cáo trạng thái,
        //          nhận/cập nhật mission, quản lý và thực thi activity
        [RoleConstants.Rescuer] =
        [
            PermissionConstants.InventorySupplyRequestCreate,
            PermissionConstants.PersonnelTeamView,
            PermissionConstants.PersonnelStatusReport,
            PermissionConstants.MissionTeamUpdate,
            PermissionConstants.MissionView,
            PermissionConstants.ActivityTeamManage,
            PermissionConstants.ActivityOwnManage,
        ],

        // Manager (Depot Manager): quản lý kho tổng + kho nhánh, xem tổng quan, quản lý thủ kho nhánh
        [RoleConstants.Manager] =
        [
            PermissionConstants.InventoryGlobalManage,
            PermissionConstants.InventoryGlobalView,
            PermissionConstants.InventoryDepotManage,
            PermissionConstants.PersonnelDepotBranchManage,
        ],

        // Victim: chỉ tạo SOS
        [RoleConstants.Victim] =
        [
            PermissionConstants.SosRequestCreate,
        ],
    };

    // ─────────────────────────────────────────────────────────────────────
    public static async Task SeedAsync(ResQDbContext context)
    {
        // 1. Upsert permissions (by code)
        foreach (var (code, name, desc) in PermissionDefs)
        {
            if (!await context.Permissions.AnyAsync(p => p.Code == code))
            {
                context.Permissions.Add(new Permission
                {
                    Code = code,
                    Name = name,
                    Description = desc
                });
            }
        }
        await context.SaveChangesAsync();

        // 2. Build code → id map
        var permMap = await context.Permissions
            .Where(p => p.Code != null)
            .ToDictionaryAsync(p => p.Code!, p => p.Id);

        // 3. Upsert role_permissions
        foreach (var (roleId, codes) in RolePermissionMap)
        {
            if (!await context.Roles.AnyAsync(r => r.Id == roleId))
                continue;

            foreach (var code in codes)
            {
                if (!permMap.TryGetValue(code, out var permId))
                    continue;

                bool exists = await context.RolePermissions
                    .AnyAsync(rp => rp.RoleId == roleId && rp.ClaimId == permId);

                if (!exists)
                {
                    context.RolePermissions.Add(new RolePermission
                    {
                        RoleId   = roleId,
                        ClaimId  = permId,
                        IsGranted = true
                    });
                }
            }
        }
        await context.SaveChangesAsync();
    }
}
