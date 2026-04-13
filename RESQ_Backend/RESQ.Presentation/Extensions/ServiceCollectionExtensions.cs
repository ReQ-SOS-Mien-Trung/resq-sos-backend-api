using Microsoft.AspNetCore.Authorization;
using RESQ.Application.Common.Constants;
using RESQ.Presentation.Authorization;

namespace RESQ.Presentation.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddAuthorization(options =>
        {
            // -- Single-permission policies --------------------------------------
            void AddSingle(string code) =>
                options.AddPolicy(code, p => p.Requirements.Add(new PermissionRequirement(code)));

            var singlePermissionPolicies = new[]
            {
                PermissionConstants.SystemConfigManage,
                PermissionConstants.SystemUserManage,
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
                PermissionConstants.InventoryGlobalManage,
                PermissionConstants.InventoryGlobalView,
                PermissionConstants.InventoryDepotManage,
                PermissionConstants.InventoryDepotPointView,
                PermissionConstants.InventorySupplyRequestCreate,
                PermissionConstants.PersonnelDepotBranchManage,
                PermissionConstants.PersonnelGlobalManage,
                PermissionConstants.PersonnelPointManage,
                PermissionConstants.PersonnelTeamView,
                PermissionConstants.PersonnelStatusReport,
                PermissionConstants.PersonnelTeamSelfView,
                PermissionConstants.PersonnelTeamAvailabilityManage,
                PermissionConstants.PersonnelAssemblyPointView,
                PermissionConstants.PersonnelAssemblyEventSelfView,
                PermissionConstants.PersonnelAssemblyEventCheckIn,
                PermissionConstants.MissionGlobalManage,
                PermissionConstants.MissionPointManage,
                PermissionConstants.MissionTeamUpdate,
                PermissionConstants.MissionView,
                PermissionConstants.MissionSelfView,
                PermissionConstants.ActivityGlobalView,
                PermissionConstants.ActivityPointView,
                PermissionConstants.ActivityTeamManage,
                PermissionConstants.ActivityOwnManage,
                PermissionConstants.ActivitySelfView,
                PermissionConstants.MissionExecutionComplete,
                PermissionConstants.MissionReportView,
                PermissionConstants.MissionReportEdit,
                PermissionConstants.MissionReportSubmit,
                PermissionConstants.MissionIncidentReport,
                PermissionConstants.MissionIncidentView,
                PermissionConstants.MissionIncidentManage,
                PermissionConstants.SosRequestCreate,
                PermissionConstants.SosRequestView,
                PermissionConstants.SosRequestCancelOwn
            };

            foreach (var code in singlePermissionPolicies)
            {
                AddSingle(code);
            }

            // -- Composite / OR-logic policies ----------------------------------
            options.AddPolicy(PermissionConstants.PolicyMissionManage, p => p.Requirements.Add(
                new PermissionRequirement(
                    PermissionConstants.MissionGlobalManage,
                    PermissionConstants.MissionPointManage)));

            options.AddPolicy(PermissionConstants.PolicyMissionAccess, p => p.Requirements.Add(
                new PermissionRequirement(
                    PermissionConstants.MissionGlobalManage,
                    PermissionConstants.MissionPointManage,
                    PermissionConstants.MissionTeamUpdate,
                    PermissionConstants.MissionView)));

            options.AddPolicy(PermissionConstants.PolicyActivityManage, p => p.Requirements.Add(
                new PermissionRequirement(
                    PermissionConstants.MissionGlobalManage,
                    PermissionConstants.MissionPointManage,
                    PermissionConstants.ActivityTeamManage)));

            options.AddPolicy(PermissionConstants.PolicyActivityAccess, p => p.Requirements.Add(
                new PermissionRequirement(
                    PermissionConstants.ActivityGlobalView,
                    PermissionConstants.ActivityPointView,
                    PermissionConstants.MissionGlobalManage,
                    PermissionConstants.MissionPointManage,
                    PermissionConstants.ActivityTeamManage,
                    PermissionConstants.ActivityOwnManage)));

            options.AddPolicy(PermissionConstants.PolicyActivityExecutionSync, p => p.Requirements.Add(
                new PermissionRequirement(
                    PermissionConstants.ActivityTeamManage,
                    PermissionConstants.ActivityOwnManage)));

            options.AddPolicy(PermissionConstants.PolicyInventoryRead, p => p.Requirements.Add(
                new PermissionRequirement(
                    PermissionConstants.InventoryGlobalManage,
                    PermissionConstants.InventoryGlobalView,
                    PermissionConstants.InventoryDepotManage,
                    PermissionConstants.InventoryDepotPointView)));

            options.AddPolicy(PermissionConstants.PolicyInventoryWrite, p => p.Requirements.Add(
                new PermissionRequirement(
                    PermissionConstants.InventoryGlobalManage,
                    PermissionConstants.InventoryDepotManage)));

            options.AddPolicy(PermissionConstants.PolicyPersonnelManage, p => p.Requirements.Add(
                new PermissionRequirement(
                    PermissionConstants.PersonnelGlobalManage,
                    PermissionConstants.PersonnelPointManage)));

            options.AddPolicy(PermissionConstants.PolicyPersonnelAccess, p => p.Requirements.Add(
                new PermissionRequirement(
                    PermissionConstants.PersonnelGlobalManage,
                    PermissionConstants.PersonnelPointManage,
                    PermissionConstants.PersonnelTeamView)));

            options.AddPolicy(PermissionConstants.PolicyDepotView, p => p.Requirements.Add(
                new PermissionRequirement(
                    PermissionConstants.InventoryGlobalManage,
                    PermissionConstants.InventoryGlobalView,
                    PermissionConstants.MissionGlobalManage,
                    PermissionConstants.MissionPointManage,
                    PermissionConstants.MissionTeamUpdate,
                    PermissionConstants.PersonnelGlobalManage,
                    PermissionConstants.PersonnelPointManage)));

            options.AddPolicy(PermissionConstants.PolicySosClusterManage, p => p.Requirements.Add(
                new PermissionRequirement(
                    PermissionConstants.MissionGlobalManage,
                    PermissionConstants.InventoryGlobalManage)));

            options.AddPolicy(PermissionConstants.PolicySosRequestAccess, p => p.Requirements.Add(
                new PermissionRequirement(
                    PermissionConstants.SosRequestView,
                    PermissionConstants.SosRequestCreate)));

            options.AddPolicy(PermissionConstants.PolicyRouteAccess, p => p.Requirements.Add(
                new PermissionRequirement(
                    PermissionConstants.MissionGlobalManage,
                    PermissionConstants.MissionPointManage,
                    PermissionConstants.MissionTeamUpdate,
                    PermissionConstants.ActivityTeamManage,
                    PermissionConstants.ActivityOwnManage)));
        });

        return services;
    }
}
