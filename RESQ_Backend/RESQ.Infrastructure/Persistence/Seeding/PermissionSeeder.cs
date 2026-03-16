using Microsoft.EntityFrameworkCore;
using RESQ.Application.Common.Constants;
using RESQ.Infrastructure.Entities.Identity;

namespace RESQ.Infrastructure.Persistence.Seeding;

public static class PermissionSeeder
{
    public static void SeedPermission(this ModelBuilder modelBuilder)
    {
        SeedPermissions(modelBuilder);
        SeedRolePermissions(modelBuilder);
    }

    private static void SeedPermissions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Permission>().HasData(
            // ── Hệ thống ──────────────────────────────────────────────────────
            new Permission { Id = 1,  Code = PermissionConstants.SystemConfigManage,           Name = "Quản lý Cấu hình Hệ thống",  Description = "Tạo/sửa/xóa Role, gán quyền động" },
            new Permission { Id = 2,  Code = PermissionConstants.SystemUserManage,             Name = "Quản lý Người dùng",          Description = "Ban/unban, tạo/sửa tài khoản, đổi role" },
            new Permission { Id = 3,  Code = PermissionConstants.SystemUserView,               Name = "Xem Danh sách User & Role",   Description = "Chỉ được xem, không sửa" },
            // ── Kho & Vật tư ──────────────────────────────────────────────────
            new Permission { Id = 4,  Code = PermissionConstants.InventoryGlobalManage,        Name = "Quản lý Kho Tổng",            Description = "Xuất/nhập/tồn, điều chuyển giữa các kho" },
            new Permission { Id = 5,  Code = PermissionConstants.InventoryGlobalView,          Name = "Xem Tổng quan Tồn kho",       Description = "Xem inventory toàn bộ kho để ra quyết định" },
            new Permission { Id = 6,  Code = PermissionConstants.InventoryDepotManage,         Name = "Quản lý Kho Nhánh",           Description = "Xuất/nhập/kiểm kê kho được giao, duyệt yêu cầu vật tư" },
            new Permission { Id = 7,  Code = PermissionConstants.InventoryDepotPointView,      Name = "Xem Tồn kho Điểm Tập kết",   Description = "Xem tồn kho tại điểm tập kết của mình" },
            new Permission { Id = 8,  Code = PermissionConstants.InventorySupplyRequestCreate, Name = "Tạo Phiếu Yêu cầu Vật tư",  Description = "Tạo phiếu yêu cầu cấp phát vật tư cho đội" },
            // ── Nhân sự ───────────────────────────────────────────────────────
            new Permission { Id = 9,  Code = PermissionConstants.PersonnelDepotBranchManage,   Name = "Quản lý Thủ kho Nhánh",       Description = "Quản lý danh sách thủ kho nhánh" },
            new Permission { Id = 10, Code = PermissionConstants.PersonnelGlobalManage,        Name = "Quản lý Nhân sự Toàn cục",   Description = "Điều phối nhân sự, tạo Team, chỉ định Core/Volunteer" },
            new Permission { Id = 11, Code = PermissionConstants.PersonnelPointManage,         Name = "Quản lý Nhân sự Điểm",        Description = "Tạo Team và phân bổ lực lượng nội bộ điểm tập kết" },
            new Permission { Id = 12, Code = PermissionConstants.PersonnelTeamView,            Name = "Xem Thành viên Team",          Description = "Xem danh sách thành viên trong Team" },
            new Permission { Id = 13, Code = PermissionConstants.PersonnelStatusReport,        Name = "Báo cáo Trạng thái Cá nhân", Description = "Báo cáo trạng thái sẵn sàng của cá nhân" },
            // ── Chiến dịch ────────────────────────────────────────────────────
            new Permission { Id = 14, Code = PermissionConstants.MissionGlobalManage,          Name = "Quản lý Chiến dịch Toàn cục", Description = "Nhận yêu cầu cứu hộ, tạo và duyệt Mission tổng" },
            new Permission { Id = 15, Code = PermissionConstants.MissionPointManage,           Name = "Quản lý Chiến dịch Điểm",    Description = "Tạo Mission cấp cơ sở, giao Mission cho Team" },
            new Permission { Id = 16, Code = PermissionConstants.MissionTeamUpdate,            Name = "Cập nhật Trạng thái Mission", Description = "Nhận Mission, cập nhật trạng thái tổng của Mission" },
            new Permission { Id = 17, Code = PermissionConstants.MissionView,                  Name = "Xem Thông tin Mission",        Description = "Xem thông tin, bối cảnh Mission của đội" },
            // ── Hoạt động ─────────────────────────────────────────────────────
            new Permission { Id = 18, Code = PermissionConstants.ActivityGlobalView,           Name = "Xem Tiến độ Chung",           Description = "Theo dõi tiến độ chung toàn hệ thống" },
            new Permission { Id = 19, Code = PermissionConstants.ActivityPointView,            Name = "Xem Tiến độ Điểm",            Description = "Theo dõi tiến độ đội nhà tại điểm" },
            new Permission { Id = 20, Code = PermissionConstants.ActivityTeamManage,           Name = "Quản lý Hoạt động Team",      Description = "Tạo Activity, assign cho Volunteer, duyệt kết quả" },
            new Permission { Id = 21, Code = PermissionConstants.ActivityOwnManage,            Name = "Quản lý Hoạt động Cá nhân",  Description = "Nhận Activity được assign, báo cáo, cập nhật trạng thái" },
            // ── Cứu hộ ────────────────────────────────────────────────────────
            new Permission { Id = 22, Code = PermissionConstants.SosRequestCreate,             Name = "Tạo Yêu cầu Cứu hộ",         Description = "Gửi yêu cầu cứu hộ khẩn cấp" },
            new Permission { Id = 23, Code = PermissionConstants.SosRequestView,               Name = "Xem Yêu cầu Cứu hộ",          Description = "Xem danh sách và chi tiết yêu cầu cứu hộ" }
        );
    }

    private static void SeedRolePermissions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RolePermission>().HasData(
            // ── Admin (1): tất cả quyền ───────────────────────────────────────
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 1,  IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 2,  IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 3,  IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 4,  IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 5,  IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 6,  IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 7,  IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 8,  IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 9,  IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 10, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 11, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 12, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 13, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 14, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 15, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 16, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 17, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 18, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 19, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 20, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 21, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 22, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Admin, ClaimId = 23, IsGranted = true },

            // ── Coordinator (2): xem user/role, xem kho tổng + điểm,
            //    quản lý nhân sự toàn cục + điểm, quản lý chiến dịch tổng + điểm,
            //    xem tiến độ chung + điểm, tạo/xem SOS ─────────────────────────
            new RolePermission { RoleId = RoleConstants.Coordinator, ClaimId = 3,  IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Coordinator, ClaimId = 5,  IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Coordinator, ClaimId = 7,  IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Coordinator, ClaimId = 10, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Coordinator, ClaimId = 11, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Coordinator, ClaimId = 14, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Coordinator, ClaimId = 15, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Coordinator, ClaimId = 18, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Coordinator, ClaimId = 19, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Coordinator, ClaimId = 22, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Coordinator, ClaimId = 23, IsGranted = true },

            // ── Rescuer (3): yêu cầu vật tư, xem team, báo cáo trạng thái,
            //    nhận/cập nhật mission, quản lý và thực thi activity ──────────
            new RolePermission { RoleId = RoleConstants.Rescuer, ClaimId = 8,  IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Rescuer, ClaimId = 12, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Rescuer, ClaimId = 13, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Rescuer, ClaimId = 16, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Rescuer, ClaimId = 17, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Rescuer, ClaimId = 20, IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Rescuer, ClaimId = 21, IsGranted = true },

            // ── Manager (4): quản lý kho tổng + kho nhánh, xem tổng quan,
            //    quản lý thủ kho nhánh ───────────────────────────────────────
            new RolePermission { RoleId = RoleConstants.Manager, ClaimId = 4,  IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Manager, ClaimId = 5,  IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Manager, ClaimId = 6,  IsGranted = true },
            new RolePermission { RoleId = RoleConstants.Manager, ClaimId = 9,  IsGranted = true },

            // ── Victim (5): chỉ tạo SOS ───────────────────────────────────────
            new RolePermission { RoleId = RoleConstants.Victim, ClaimId = 22, IsGranted = true }
        );
    }
}
