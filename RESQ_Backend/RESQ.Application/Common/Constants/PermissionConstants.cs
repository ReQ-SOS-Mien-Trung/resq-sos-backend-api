namespace RESQ.Application.Common.Constants;

/// <summary>
/// Mã quy?n h? th?ng. M?i const là m?t policy name dùng trong [Authorize(Policy = ...)].
/// Composite policy (OR logic) du?c khai báo riêng ? nhóm "Policy*".
/// </summary>
public static class PermissionConstants
{
    // -- C?u hình & H? th?ng ------------------------------------------
    /// <summary>Admin: T?o/s?a/xóa Role, gán quy?n d?ng</summary>
    public const string SystemConfigManage = "system.config.manage";

    /// <summary>Admin: Ban/unban, t?o/s?a tài kho?n, thay d?i role</summary>
    public const string SystemUserManage = "system.user.manage";

    /// <summary>Admin + Coordinator_Global: Ch? xem danh sách Role/User</summary>
    public const string SystemUserView = "system.user.view";

    // -- Identity / Self-service --------------------------------------
    /// <summary>Ngu?i dùng dã dang nh?p: Xem h? so và thông tin hi?n t?i c?a chính mình</summary>
    public const string IdentitySelfView = "identity.self.view";

    /// <summary>Ngu?i dùng dã dang nh?p: C?p nh?t h? so cá nhân c?a chính mình</summary>
    public const string IdentityProfileUpdate = "identity.profile.update";

    /// <summary>Ngu?i dùng dã dang nh?p: Ðang ký và g? token nh?n notification c?a chính mình</summary>
    public const string IdentityNotificationDeviceManage = "identity.notification.device.manage";

    /// <summary>Ngu?i dùng dã dang nh?p: Xem h? so ngu?i thân c?a chính mình</summary>
    public const string IdentityRelativeProfileView = "identity.relative_profile.view";

    /// <summary>Ngu?i dùng dã dang nh?p: Qu?n lý h? so ngu?i thân c?a chính mình</summary>
    public const string IdentityRelativeProfileManage = "identity.relative_profile.manage";

    /// <summary>Ngu?i dùng dã dang nh?p: Qu?n lý phiên dang nh?p c?a chính mình</summary>
    public const string IdentitySessionManage = "identity.session.manage";

    // -- Notification / Chat self-service -----------------------------
    /// <summary>Ngu?i dùng dã dang nh?p: Xem notification c?a chính mình</summary>
    public const string NotificationSelfView = "notification.self.view";

    /// <summary>Ngu?i dùng dã dang nh?p: Ðánh d?u và qu?n lý notification c?a chính mình</summary>
    public const string NotificationSelfManage = "notification.self.manage";

    /// <summary>Participant h?p l?: Xem conversation và l?ch s? chat c?a chính mình</summary>
    public const string ConversationSelfView = "conversation.self.view";

    /// <summary>Participant h?p l?: G?i tin nh?n và thao tác trong conversation c?a chính mình</summary>
    public const string ConversationSelfManage = "conversation.self.manage";

    /// <summary>Coordinator/Admin: Nh?n phòng ch? và di?u ph?i conversation h? tr?</summary>
    public const string ConversationCoordinatorManage = "conversation.coordinator.manage";

    // -- Qu?n lý Kho & v?t ph?m (Inventory / Logistics) -----------------
    /// <summary>Admin + DepotManager: Toàn quy?n xu?t/nh?p/t?n, di?u chuy?n gi?a các kho du?c phân công</summary>
    public const string InventoryGlobalManage = "inventory.global.manage";

    /// <summary>Admin + Coordinator + DepotManager: Xem t?ng quan t?n kho</summary>
    public const string InventoryGlobalView = "inventory.global.view";

    /// <summary>Admin + Coordinator_Point: Xem t?n kho t?i di?m t?p k?t c?a mình</summary>
    public const string InventoryDepotPointView = "inventory.depot_point.view";

    /// <summary>Admin + Rescuer_Core: T?o phi?u yêu c?u c?p phát v?t ph?m cho d?i</summary>
    public const string InventorySupplyRequestCreate = "inventory.supply_request.create";

    // -- Qu?n lý Ð?i nhóm (Teams / Personnel) -------------------------
    /// <summary>Admin + DepotManager: Qu?n lý danh sách th? kho nhánh</summary>
    public const string PersonnelDepotBranchManage = "personnel.depot_branch.manage";

    /// <summary>Admin + Coordinator_Global: Toàn quy?n di?u ph?i nhân s?, t?o Team, ch? d?nh Core/Volunteer</summary>
    public const string PersonnelGlobalManage = "personnel.global.manage";

    /// <summary>Admin + Coordinator_Point: T?o Team và phân b? l?c lu?ng n?i b? di?m t?p k?t</summary>
    public const string PersonnelPointManage = "personnel.point.manage";

    /// <summary>Admin + Rescuer_Core: Xem danh sách thành viên trong Team</summary>
    public const string PersonnelTeamView = "personnel.team.view";

    /// <summary>Admin + Rescuer_Volunteer: Báo cáo tr?ng thái s?n sàng c?a cá nhân</summary>
    public const string PersonnelStatusReport = "personnel.status.report";

    /// <summary>Admin + Coordinator + Rescuer: Xem d?i hi?n t?i c?a chính mình</summary>
    public const string PersonnelTeamSelfView = "personnel.team.self.view";

    /// <summary>Admin + Coordinator + Rescuer: Qu?n lý tr?ng thái s?n sàng c?a d?i</summary>
    public const string PersonnelTeamAvailabilityManage = "personnel.team.availability.manage";

    /// <summary>Admin + Coordinator + Rescuer: Xem di?m t?p k?t ph?c v? mobile rescuer</summary>
    public const string PersonnelAssemblyPointView = "personnel.assembly_point.view";

    /// <summary>Admin + Coordinator + Rescuer: Xem s? ki?n t?p trung c?a chính mình</summary>
    public const string PersonnelAssemblyEventSelfView = "personnel.assembly_event.self.view";

    /// <summary>Admin + Coordinator + Rescuer: Check-in vào s? ki?n t?p trung c?a chính mình</summary>
    public const string PersonnelAssemblyEventCheckIn = "personnel.assembly_event.checkin";

    // -- Ði?u ph?i Chi?n d?ch (Missions / Operations) -----------------
    /// <summary>Admin + Coordinator_Global: Nh?n yêu c?u c?u h?, t?o và duy?t Mission t?ng</summary>
    public const string MissionGlobalManage = "mission.global.manage";

    /// <summary>Admin + Coordinator_Point: T?o Mission c?p co s?, giao Mission cho Team thu?c di?m</summary>
    public const string MissionPointManage = "mission.point.manage";

    /// <summary>Admin + Rescuer_Core: Nh?n Mission, c?p nh?t tr?ng thái t?ng c?a Mission</summary>
    public const string MissionTeamUpdate = "mission.team.update";

    /// <summary>Admin + Rescuer_Core + Rescuer_Volunteer: Xem thông tin, b?i c?nh Mission c?a d?i</summary>
    public const string MissionView = "mission.view";

    /// <summary>Admin + Coordinator + Rescuer: Xem mission c?a d?i hi?n t?i</summary>
    public const string MissionSelfView = "mission.self.view";

    // -- Th?c thi Th?c d?a (Activities) -------------------------------
    /// <summary>Admin + Coordinator_Global: Theo dõi ti?n d? chung toàn h? th?ng</summary>
    public const string ActivityGlobalView = "activity.global.view";

    /// <summary>Admin + Coordinator_Point: Theo dõi ti?n d? d?i nhà t?i di?m</summary>
    public const string ActivityPointView = "activity.point.view";

    /// <summary>Admin + Rescuer_Core: T?o Activity, assign cho Volunteer, duy?t k?t qu? (trong Team)</summary>
    public const string ActivityTeamManage = "activity.team.manage";

    /// <summary>Admin + Rescuer_Volunteer: Nh?n Activity du?c assign, báo cáo, c?p nh?t tr?ng thái</summary>
    public const string ActivityOwnManage = "activity.own.manage";

    /// <summary>Admin + Coordinator + Rescuer: Xem activity c?a d?i hi?n t?i</summary>
    public const string ActivitySelfView = "activity.self.view";

    /// <summary>Admin + Coordinator + Rescuer: Xác nh?n hoàn t?t th?c thi c?a mission team</summary>
    public const string MissionExecutionComplete = "mission.execution.complete";

    /// <summary>Admin + Coordinator + Rescuer: Xem báo cáo mission team</summary>
    public const string MissionReportView = "mission.report.view";

    /// <summary>Admin + Coordinator + Rescuer: Luu/ch?nh s?a draft báo cáo mission team</summary>
    public const string MissionReportEdit = "mission.report.edit";

    /// <summary>Admin + Coordinator + Rescuer: N?p báo cáo mission team</summary>
    public const string MissionReportSubmit = "mission.report.submit";

    /// <summary>Admin + Coordinator + Rescuer: Báo incident trong mission/activity</summary>
    public const string MissionIncidentReport = "mission.incident.report";

    /// <summary>Admin + Coordinator + Rescuer: Xem incident c?a mission/team</summary>
    public const string MissionIncidentView = "mission.incident.view";

    /// <summary>Admin + Coordinator + Rescuer: Qu?n lý tr?ng thái incident</summary>
    public const string MissionIncidentManage = "mission.incident.manage";

    // -- SOS -----------------------------------------------------------
    /// <summary>Admin + Coordinator_Global + Victim: G?i yêu c?u c?u h? kh?n c?p</summary>
    public const string SosRequestCreate = "sos.request.create";

    /// <summary>Admin + Coordinator_Global: Xem danh sách và chi ti?t yêu c?u c?u h?</summary>
    public const string SosRequestView = "sos.request.view";

    /// <summary>Victim: Truy c?p endpoint hu? SOS c?a mình; domain v?n ki?m tra owner/companion c? th?</summary>
    public const string SosRequestCancelOwn = "sos.request.cancel.own";

    // -- Combined / Composite policy names (OR logic) -----------------
    // Dùng khi m?t endpoint c?n cho phép nhi?u role khác nhau.

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
