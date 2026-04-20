using RESQ.Application.Common.Constants;
using RESQ.Infrastructure.Persistence.Seeding;

namespace RESQ.Tests.Infrastructure.Seeding;

public class PermissionSeederTests
{
    [Fact]
    public void CreateRolePermissions_GrantsAssemblyPointViewToVictim()
    {
        var permissions = PermissionSeeder.CreatePermissions()
            .ToDictionary(permission => permission.Code!, permission => permission.Id);
        var rolePermissions = PermissionSeeder.CreateRolePermissions();

        Assert.Contains(rolePermissions, rolePermission =>
            rolePermission.RoleId == RoleConstants.Victim
            && rolePermission.ClaimId == permissions[PermissionConstants.PersonnelAssemblyPointView]
            && rolePermission.IsGranted == true);
    }
}
