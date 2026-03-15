using Microsoft.AspNetCore.Authorization;
using RESQ.Application.Common.Constants;
using RESQ.Presentation.Authorization;

namespace RESQ.Presentation.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddAuthorization(options =>
        {
            // ── Single-permission policies ──────────────────────────────────────
            void AddSingle(string code) =>
                options.AddPolicy(code, p => p.Requirements.Add(new PermissionRequirement(code)));

            AddSingle(PermissionConstants.SystemConfigManage);
            AddSingle(PermissionConstants.SystemUserManage);
            AddSingle(PermissionConstants.SystemUserView);
            AddSingle(PermissionConstants.InventoryGlobalManage);
            AddSingle(PermissionConstants.InventoryGlobalView);
            AddSingle(PermissionConstants.InventoryDepotManage);
            AddSingle(PermissionConstants.InventoryDepotPointView);
            AddSingle(PermissionConstants.InventorySupplyRequestCreate);
            AddSingle(PermissionConstants.PersonnelDepotBranchManage);
            AddSingle(PermissionConstants.PersonnelGlobalManage);
            AddSingle(PermissionConstants.PersonnelPointManage);
            AddSingle(PermissionConstants.PersonnelTeamView);
            AddSingle(PermissionConstants.PersonnelStatusReport);
            AddSingle(PermissionConstants.MissionGlobalManage);
            AddSingle(PermissionConstants.MissionPointManage);
            AddSingle(PermissionConstants.MissionTeamUpdate);
            AddSingle(PermissionConstants.MissionView);
            AddSingle(PermissionConstants.ActivityGlobalView);
            AddSingle(PermissionConstants.ActivityPointView);
            AddSingle(PermissionConstants.ActivityTeamManage);
            AddSingle(PermissionConstants.ActivityOwnManage);
            AddSingle(PermissionConstants.SosRequestCreate);
            AddSingle(PermissionConstants.SosRequestView);

            // ── Composite / OR-logic policies ──────────────────────────────────
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
