using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Identity.Commands.AssignRoleToUser;
using RESQ.Application.UseCases.Identity.Commands.SetUserAvatarUrl;
using RESQ.Application.UseCases.Identity.Commands.SetUserPermissions;
using RESQ.Application.UseCases.Identity.Queries.GetUserPermissions;
using RESQ.Domain.Entities.Identity;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Identity;

/// <summary>
/// Tests for AssignRoleToUser, SetUserAvatarUrl, GetUserPermissions, and SetUserPermissions handlers.
/// </summary>
public sealed class RoleAvatarPermissionsHandlerTests
{
    // ──────────────────────── AssignRoleToUser ────────────────────────

    [Fact]
    public async Task AssignRoleToUser_Success_UpdatesRoleId()
    {
        var user = BuildUser();
        user.RoleId = 2;
        var repo = new StubUserRepository(user);
        var roleRepo = new StubRoleRepository(new RoleModel { Id = 3, Name = "Rescuer" });
        var uow = new StubUnitOfWork();
        var handler = new AssignRoleToUserCommandHandler(repo, roleRepo, uow, NullLogger<AssignRoleToUserCommandHandler>.Instance);

        var cmd = new AssignRoleToUserCommand(UserId: user.Id, RoleId: 3);
        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(user.Id, res.UserId);
        Assert.Equal(3, res.RoleId);
        Assert.Equal(3, user.RoleId);
        Assert.Equal(1, uow.SaveCalls);
    }

    [Fact]
    public async Task AssignRoleToUser_UserNotFound_ThrowsNotFoundException()
    {
        var repo = new StubUserRepository();
        var roleRepo = new StubRoleRepository(new RoleModel { Id = 3, Name = "Rescuer" });
        var uow = new StubUnitOfWork();
        var handler = new AssignRoleToUserCommandHandler(repo, roleRepo, uow, NullLogger<AssignRoleToUserCommandHandler>.Instance);

        var cmd = new AssignRoleToUserCommand(UserId: Guid.NewGuid(), RoleId: 3);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task AssignRoleToUser_RoleNotFound_ThrowsNotFoundException()
    {
        var user = BuildUser();
        var repo = new StubUserRepository(user);
        var roleRepo = new StubRoleRepository(); // no roles
        var uow = new StubUnitOfWork();
        var handler = new AssignRoleToUserCommandHandler(repo, roleRepo, uow, NullLogger<AssignRoleToUserCommandHandler>.Instance);

        var cmd = new AssignRoleToUserCommand(UserId: user.Id, RoleId: 999);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    // ──────────────────────── SetUserAvatarUrl ────────────────────────

    [Fact]
    public async Task SetUserAvatarUrl_Success_UpdatesAvatar()
    {
        var user = BuildUser();
        var repo = new StubUserRepository(user);
        var uow = new StubUnitOfWork();
        var handler = new SetUserAvatarUrlCommandHandler(repo, uow, NullLogger<SetUserAvatarUrlCommandHandler>.Instance);

        var cmd = new SetUserAvatarUrlCommand(UserId: user.Id, AvatarUrl: "https://cdn.example/new.png");
        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(user.Id, res.UserId);
        Assert.Equal("https://cdn.example/new.png", res.AvatarUrl);
        Assert.Equal(1, uow.SaveCalls);
    }

    [Fact]
    public async Task SetUserAvatarUrl_UserNotFound_ThrowsNotFoundException()
    {
        var repo = new StubUserRepository();
        var uow = new StubUnitOfWork();
        var handler = new SetUserAvatarUrlCommandHandler(repo, uow, NullLogger<SetUserAvatarUrlCommandHandler>.Instance);

        var cmd = new SetUserAvatarUrlCommand(UserId: Guid.NewGuid(), AvatarUrl: "https://cdn.example/a.png");
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task SetUserAvatarUrl_SaveFails_ThrowsCreateFailedException()
    {
        var user = BuildUser();
        var repo = new StubUserRepository(user);
        var uow = new FailingSaveUnitOfWork();
        var handler = new SetUserAvatarUrlCommandHandler(repo, uow, NullLogger<SetUserAvatarUrlCommandHandler>.Instance);

        var cmd = new SetUserAvatarUrlCommand(UserId: user.Id, AvatarUrl: "https://cdn.example/a.png");
        await Assert.ThrowsAsync<CreateFailedException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    // ──────────────────────── GetUserPermissions ────────────────────────

    [Fact]
    public async Task GetUserPermissions_Success_ReturnsPermissions()
    {
        var user = BuildUser();
        var repo = new StubUserRepository(user);
        var permRepo = new StubPermissionRepository(
        [
            new PermissionModel { Id = 1, Code = "IdentitySelfView", Name = "Self View", Description = "View own profile" },
            new PermissionModel { Id = 2, Code = "MissionView", Name = "Mission View", Description = "View missions" }
        ]);
        var handler = new GetUserPermissionsQueryHandler(repo, permRepo);

        var query = new GetUserPermissionsQuery(UserId: user.Id);
        var res = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(user.Id, res.UserId);
        Assert.Equal(2, res.Permissions.Count);
        Assert.Equal("IdentitySelfView", res.Permissions[0].Code);
        Assert.Equal("MissionView", res.Permissions[1].Code);
    }

    [Fact]
    public async Task GetUserPermissions_UserNotFound_ThrowsNotFoundException()
    {
        var repo = new StubUserRepository();
        var permRepo = new StubPermissionRepository([]);
        var handler = new GetUserPermissionsQueryHandler(repo, permRepo);

        var query = new GetUserPermissionsQuery(UserId: Guid.NewGuid());
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(query, CancellationToken.None));
    }

    // ──────────────────────── SetUserPermissions ────────────────────────

    [Fact]
    public async Task SetUserPermissions_Success_SetsPermissionsAndSaves()
    {
        var user = BuildUser();
        var repo = new StubUserRepository(user);
        var permRepo = new StubPermissionRepository([]);
        var uow = new StubUnitOfWork();
        var handler = new SetUserPermissionsCommandHandler(repo, permRepo, uow);

        var adminId = Guid.NewGuid();
        var cmd = new SetUserPermissionsCommand(
            TargetUserId: user.Id,
            AdminId: adminId,
            PermissionIds: [1, 2, 3]);

        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(user.Id, res.UserId);
        Assert.Equal([1, 2, 3], res.PermissionIds);
        Assert.Equal(1, uow.SaveCalls);
        Assert.Equal(user.Id, permRepo.LastSetUserId);
        Assert.Equal(adminId, permRepo.LastSetGrantedBy);
        Assert.Equal(new List<int> { 1, 2, 3 }, permRepo.LastSetPermissionIds);
    }

    [Fact]
    public async Task SetUserPermissions_UserNotFound_ThrowsNotFoundException()
    {
        var repo = new StubUserRepository();
        var permRepo = new StubPermissionRepository([]);
        var uow = new StubUnitOfWork();
        var handler = new SetUserPermissionsCommandHandler(repo, permRepo, uow);

        var cmd = new SetUserPermissionsCommand(TargetUserId: Guid.NewGuid(), AdminId: Guid.NewGuid(), PermissionIds: [1]);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    // ──────────────────────── helpers ────────────────────────

    private static UserModel BuildUser() => new()
    {
        Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        Email = "user@example.com",
        Username = "user@example.com",
        Password = BCrypt.Net.BCrypt.HashPassword("P@ssw0rd!"),
        RoleId = 2,
    };

    // ──────────────────────── stubs ────────────────────────

    private sealed class StubUserRepository : IUserRepository
    {
        private readonly Dictionary<Guid, UserModel> _usersById = [];
        public UserModel? LastUpdatedUser { get; private set; }

        public StubUserRepository(params UserModel[] seeds)
        {
            foreach (var s in seeds) _usersById[s.Id] = s;
        }

        public Task<UserModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_usersById.GetValueOrDefault(id));
        public Task<UserModel?> GetByUsernameAsync(string u, CancellationToken ct = default)
            => Task.FromResult(_usersById.Values.FirstOrDefault(x => x.Username == u));
        public Task<UserModel?> GetByEmailAsync(string e, CancellationToken ct = default)
            => Task.FromResult(_usersById.Values.FirstOrDefault(x => x.Email == e));
        public Task<UserModel?> GetByPhoneAsync(string p, CancellationToken ct = default)
            => Task.FromResult(_usersById.Values.FirstOrDefault(x => x.Phone == p));
        public Task<List<UserModel>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
            => Task.FromResult(_usersById.Where(kv => ids.Contains(kv.Key)).Select(kv => kv.Value).ToList());
        public Task<UserModel?> GetByEmailVerificationTokenAsync(string t, CancellationToken ct = default)
            => Task.FromResult<UserModel?>(null);
        public Task<UserModel?> GetByPasswordResetTokenAsync(string t, CancellationToken ct = default)
            => Task.FromResult<UserModel?>(null);
        public Task CreateAsync(UserModel user, CancellationToken ct = default)
        { _usersById[user.Id] = user; return Task.CompletedTask; }
        public Task UpdateAsync(UserModel user, CancellationToken ct = default)
        { LastUpdatedUser = user; _usersById[user.Id] = user; return Task.CompletedTask; }
        public Task<PagedResult<UserModel>> GetPagedAsync(int pn, int ps, int? r = null, bool? b = null, string? s = null, int? er = null, bool? ie = null, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<PagedResult<UserModel>> GetPagedForPermissionAsync(int pn, int ps, int? r = null, string? s = null, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<List<Guid>> GetActiveAdminUserIdsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<AvailableManagerDto>> GetAvailableManagersAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class StubRoleRepository : IRoleRepository
    {
        private readonly Dictionary<int, RoleModel> _roles = [];

        public StubRoleRepository(params RoleModel[] seeds)
        {
            foreach (var r in seeds) _roles[r.Id] = r;
        }

        public Task<RoleModel?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult(_roles.GetValueOrDefault(id));
        public Task<List<RoleModel>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<RoleModel?> GetByNameAsync(string name, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CreateAsync(RoleModel model, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(RoleModel model, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<PermissionModel>> GetPermissionsAsync(int roleId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SetPermissionsAsync(int roleId, List<int> permissionIds, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class StubPermissionRepository : IPermissionRepository
    {
        private readonly List<PermissionModel> _userPermissions;
        public Guid? LastSetUserId { get; private set; }
        public Guid? LastSetGrantedBy { get; private set; }
        public List<int>? LastSetPermissionIds { get; private set; }

        public StubPermissionRepository(List<PermissionModel> userPermissions)
        {
            _userPermissions = userPermissions;
        }

        public Task<List<PermissionModel>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(_userPermissions.ToList());
        public Task SetUserPermissionsAsync(Guid userId, Guid grantedBy, List<int> permissionIds, CancellationToken ct = default)
        {
            LastSetUserId = userId;
            LastSetGrantedBy = grantedBy;
            LastSetPermissionIds = permissionIds;
            return Task.CompletedTask;
        }
        public Task<List<string>> GetEffectivePermissionCodesAsync(Guid userId, int? roleId, CancellationToken ct = default)
            => Task.FromResult(_userPermissions.Select(p => p.Code!).ToList());
        public Task<List<PermissionModel>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<PermissionModel?> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<PermissionModel?> GetByCodeAsync(string code, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CreateAsync(PermissionModel model, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(PermissionModel model, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FailingSaveUnitOfWork : IUnitOfWork
    {
        public IGenericRepository<T> GetRepository<T>() where T : class => throw new NotImplementedException();
        public IQueryable<T> Set<T>() where T : class => throw new NotImplementedException();
        public IQueryable<T> SetTracked<T>() where T : class => throw new NotImplementedException();
        public int SaveChangesWithTransaction() => 0;
        public Task<int> SaveChangesWithTransactionAsync() => Task.FromResult(0);
        public Task<int> SaveAsync() => Task.FromResult(0);
        public void AttachAsUnchanged<TEntity>(TEntity entity) where TEntity : class { }
        public void ClearTrackedChanges() { }
        public Task ExecuteInTransactionAsync(Func<Task> action) => action();
    }
}
