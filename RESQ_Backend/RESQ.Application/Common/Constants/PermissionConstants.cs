namespace RESQ.Application.Common.Constants;

/// <summary>
/// Mã quyền hệ thống. Mỗi const là một policy name dùng trong [Authorize(Policy = ...)].
/// Composite policy (OR logic) được khai báo riêng ở nhóm "Policy*".
/// </summary>
public static class PermissionConstants
{
    // -- Cấu hình & Hệ thống ------------------------------------------
    /// <summary>Admin: Tạo/sửa/xóa Role, gán quyền động</summary>
    public const string SystemConfigManage = "system.config.manage";

    /// <summary>Admin: Ban/unban, tạo/sửa tài khoản, thay đổi role</summary>
    public const string SystemUserManage = "system.user.manage";

    /// <summary>Admin + Coordinator_Global: Chỉ xem danh sách Role/User</summary>
    public const string SystemUserView = "system.user.view";

    // -- Identity / Self-service --------------------------------------
    /// <summary>Người dùng đã đăng nhập: Xem hồ sơ và thông tin hiện tại của chính mình</summary>
    public const string IdentitySelfView = "identity.self.view";

    /// <summary>Người dùng đã đăng nhập: Cập nhật hồ sơ cá nhân của chính mình</summary>
    public const string IdentityProfileUpdate = "identity.profile.update";

    /// <summary>Người dùng đã đăng nhập: Đăng ký và gỡ token nhận notification của chính mình</summary>
    public const string IdentityNotificationDeviceManage = "identity.notification.device.manage";

    /// <summary>Người dùng đã đăng nhập: Xem hồ sơ người thân của chính mình</summary>
    public const string IdentityRelativeProfileView = "identity.relative_profile.view";

    /// <summary>Người dùng đã đăng nhập: Quản lý hồ sơ người thân của chính mình</summary>
    public const string IdentityRelativeProfileManage = "identity.relative_profile.manage";

    /// <summary>Người dùng đã đăng nhập: Quản lý phiên đăng nhập của chính mình</summary>
    public const string IdentitySessionManage = "identity.session.manage";

    // -- Notification / Chat self-service -----------------------------
    /// <summary>Người dùng đã đăng nhập: Xem notification của chính mình</summary>
    public const string NotificationSelfView = "notification.self.view";

    /// <summary>Người dùng đã đăng nhập: Đánh dấu và quản lý notification của chính mình</summary>
    public const string NotificationSelfManage = "notification.self.manage";

    /// <summary>Participant hợp lệ: Xem conversation và lịch sử chat của chính mình</summary>
    public const string ConversationSelfView = "conversation.self.view";

    /// <summary>Participant hợp lệ: Gửi tin nhắn và thao tác trong conversation của chính mình</summary>
    public const string ConversationSelfManage = "conversation.self.manage";

    /// <summary>Coordinator/Admin: Nhận phòng chờ và điều phối conversation hỗ trợ</summary>
    public const string ConversationCoordinatorManage = "conversation.coordinator.manage";

    // -- Quản lý Kho & vật phẩm (Inventory / Logistics) -----------------
    /// <summary>Admin + DepotManager: Toàn quyền xuất/nhập/tồn, điều chuyển giữa các kho được phân công</summary>
    public const string InventoryGlobalManage = "inventory.global.manage";

    /// <summary>Admin + Coordinator + DepotManager: Xem t?ng quan t?n kho</summary>
    public const string InventoryGlobalView = "inventory.global.view";

    /// <summary>Admin + Coordinator_Point: Xem tồn kho tại điểm tập kết của mình</summary>
    public const string InventoryDepotPointView = "inventory.depot_point.view";

    /// <summary>Admin + Rescuer_Core: Tạo phiếu yêu cầu cấp phát vật phẩm cho đội</summary>
    public const string InventorySupplyRequestCreate = "inventory.supply_request.create";

    // -- Quản lý Đội nhóm (Teams / Personnel) -------------------------
    /// <summary>Admin + DepotManager: Quản lý danh sách thủ kho nhánh</summary>
    public const string PersonnelDepotBranchManage = "personnel.depot_branch.manage";

    /// <summary>Admin + Coordinator_Global: Toàn quyền điều phối nhân sự, tạo Team, chỉ định Core/Volunteer</summary>
    public const string PersonnelGlobalManage = "personnel.global.manage";

    /// <summary>Admin + Coordinator_Point: Tạo Team và phân bổ lực lượng nội bộ điểm tập kết</summary>
    public const string PersonnelPointManage = "personnel.point.manage";

    /// <summary>Admin + Rescuer_Core: Xem danh sách thành viên trong Team</summary>
    public const string PersonnelTeamView = "personnel.team.view";

    /// <summary>Admin + Rescuer_Volunteer: Báo cáo trạng thái sẵn sàng của cá nhân</summary>
    public const string PersonnelStatusReport = "personnel.status.report";

    /// <summary>Admin + Coordinator + Rescuer: Xem đội hiện tại của chính mình</summary>
    public const string PersonnelTeamSelfView = "personnel.team.self.view";

    /// <summary>Admin + Coordinator + Rescuer: Quản lý trạng thái sẵn sàng của đội</summary>
    public const string PersonnelTeamAvailabilityManage = "personnel.team.availability.manage";

    /// <summary>Admin + Coordinator + Rescuer: Xem di?m t?p k?t ph?c v? mobile rescuer</summary>
    public const string PersonnelAssemblyPointView = "personnel.assembly_point.view";

    /// <summary>Admin + Coordinator + Rescuer: Xem sự kiện tập trung của chính mình</summary>
    public const string PersonnelAssemblyEventSelfView = "personnel.assembly_event.self.view";

    /// <summary>Admin + Coordinator + Rescuer: Check-in vào sự kiện tập trung của chính mình</summary>
    public const string PersonnelAssemblyEventCheckIn = "personnel.assembly_event.checkin";

    // -- Điều phối Chiến dịch (Missions / Operations) -----------------
    /// <summary>Admin + Coordinator_Global: Nhận yêu cầu cứu hộ, tạo và duyệt Mission tổng</summary>
    public const string MissionGlobalManage = "mission.global.manage";

    /// <summary>Admin + Coordinator_Point: T?o Mission c?p co s?, giao Mission cho Team thu?c di?m</summary>
    public const string MissionPointManage = "mission.point.manage";

    /// <summary>Admin + Rescuer_Core: Nhận Mission, cập nhật trạng thái tổng của Mission</summary>
    public const string MissionTeamUpdate = "mission.team.update";

    /// <summary>Admin + Rescuer_Core + Rescuer_Volunteer: Xem thông tin, bối cảnh Mission của đội</summary>
    public const string MissionView = "mission.view";

    /// <summary>Admin + Coordinator + Rescuer: Xem mission c?a d?i hi?n t?i</summary>
    public const string MissionSelfView = "mission.self.view";

    // -- Th?c thi Th?c d?a (Activities) -------------------------------
    /// <summary>Admin + Coordinator_Global: Theo dõi tiến độ chung toàn hệ thống</summary>
    public const string ActivityGlobalView = "activity.global.view";

    /// <summary>Admin + Coordinator_Point: Theo dõi tiến độ đội nhà tại điểm</summary>
    public const string ActivityPointView = "activity.point.view";

    /// <summary>Admin + Rescuer_Core: T?o Activity, assign cho Volunteer, duy?t k?t qu? (trong Team)</summary>
    public const string ActivityTeamManage = "activity.team.manage";

    /// <summary>Admin + Rescuer_Volunteer: Nhận Activity được assign, báo cáo, cập nhật trạng thái</summary>
    public const string ActivityOwnManage = "activity.own.manage";

    /// <summary>Admin + Coordinator + Rescuer: Xem activity c?a d?i hi?n t?i</summary>
    public const string ActivitySelfView = "activity.self.view";

    /// <summary>Admin + Coordinator + Rescuer: Xác nhận hoàn tất thực thi của mission team</summary>
    public const string MissionExecutionComplete = "mission.execution.complete";

    /// <summary>Admin + Coordinator + Rescuer: Xem báo cáo mission team</summary>
    public const string MissionReportView = "mission.report.view";

    /// <summary>Admin + Coordinator + Rescuer: Lưu/chỉnh sửa draft báo cáo mission team</summary>
    public const string MissionReportEdit = "mission.report.edit";

    /// <summary>Admin + Coordinator + Rescuer: Nộp báo cáo mission team</summary>
    public const string MissionReportSubmit = "mission.report.submit";

    /// <summary>Admin + Coordinator + Rescuer: Báo incident trong mission/activity</summary>
    public const string MissionIncidentReport = "mission.incident.report";

    /// <summary>Admin + Coordinator + Rescuer: Xem incident c?a mission/team</summary>
    public const string MissionIncidentView = "mission.incident.view";

    /// <summary>Admin + Coordinator + Rescuer: Quản lý trạng thái incident</summary>
    public const string MissionIncidentManage = "mission.incident.manage";

    // -- SOS -----------------------------------------------------------
    /// <summary>Admin + Coordinator_Global + Victim: Gửi yêu cầu cứu hộ khẩn cấp</summary>
    public const string SosRequestCreate = "sos.request.create";

    /// <summary>Admin + Coordinator_Global: Xem danh sách và chi tiết yêu cầu cứu hộ</summary>
    public const string SosRequestView = "sos.request.view";

    /// <summary>Victim: Truy cập endpoint huỷ SOS của mình; domain vẫn kiểm tra owner/companion cụ thể</summary>
    public const string SosRequestCancelOwn = "sos.request.cancel.own";

    // -- Combined / Composite policy names (OR logic) -----------------
    // Dùng khi một endpoint cần cho phép nhiều role khác nhau.

    /// <summary>MissionGlobalManage | MissionPointManage</summary>
    public const string PolicyMissionManage = "policy.mission.manage";

    /// <summary>MissionGlobalManage | MissionPointManage | MissionTeamUpdate | MissionView</summary>
    public const string PolicyMissionAccess = "policy.mission.access";

    /// <summary>MissionGlobalManage | MissionPointManage | ActivityTeamManage</summary>
    public const string PolicyActivityManage = "policy.activity.manage";

    /// <summary>ActivityGlobalView | ActivityPointView | MissionGlobalManage | MissionPointManage | ActivityTeamManage | ActivityOwnManage</summary>
    public const string PolicyActivityAccess = "policy.activity.access";

    /// <summary>ActivityTeamManage | ActivityOwnManage</summary>
    public const string PolicyActivityExecutionSync = "policy.activity.execution.sync";

    /// <summary>InventoryGlobalManage | InventoryGlobalView | InventoryDepotPointView</summary>
    public const string PolicyInventoryRead = "policy.inventory.read";

    /// <summary>InventoryGlobalManage</summary>
    public const string PolicyInventoryWrite = "policy.inventory.write";

    /// <summary>PersonnelGlobalManage | PersonnelPointManage</summary>
    public const string PolicyPersonnelManage = "policy.personnel.manage";

    /// <summary>PersonnelGlobalManage | PersonnelPointManage | PersonnelTeamView</summary>
    public const string PolicyPersonnelAccess = "policy.personnel.access";

    /// <summary>InventoryGlobalManage | InventoryGlobalView | MissionGlobalManage | MissionPointManage | MissionTeamUpdate | PersonnelGlobalManage | PersonnelPointManage</summary>
    public const string PolicyDepotView = "policy.depot.view";

    /// <summary>MissionGlobalManage | InventoryGlobalManage</summary>
    public const string PolicySosClusterManage = "policy.sos.cluster.manage";

    /// <summary>SosRequestView | SosRequestCreate (coordinator + victim view detail)</summary>
    public const string PolicySosRequestAccess = "policy.sos.request.access";

    /// <summary>MissionGlobalManage | MissionPointManage | MissionTeamUpdate | ActivityTeamManage | ActivityOwnManage</summary>
    public const string PolicyRouteAccess = "policy.route.access";
}
