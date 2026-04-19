using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Identity.Commands.AdminCreateUser;
using RESQ.Application.UseCases.Identity.Commands.AdminUpdateUser;
using RESQ.Domain.Entities.Identity;
using RESQ.Domain.Enum.Identity;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Identity;

/// <summary>
/// Tests for AdminCreateUser and AdminUpdateUser handlers.
/// </summary>
public sealed class AdminUserManagementHandlerTests
{
    // ──────────────────────── AdminCreateUser ────────────────────────

    [Fact]
    public async Task AdminCreateUser_Success_CreatesUserWithHashedPassword()
    {
        var repo = new StubUserRepository();
        var uow = new StubUnitOfWork();
        var handler = new AdminCreateUserCommandHandler(repo, uow, NullLogger<AdminCreateUserCommandHandler>.Instance);

        var cmd = new AdminCreateUserCommand(
            Phone: "0901234567",
            Email: "new@example.com",
            FirstName: "Lan",
            LastName: "Nguyen",
            Username: "lannguyen",
            Password: "Str0ngP@ss",
            RoleId: 2,
            RescuerType: "Volunteer",
            AvatarUrl: null,
            Address: "District 1",
            Ward: "Ben Nghe",
            Province: "HCM",
            Latitude: 10.78,
            Longitude: 106.70,
            IsEmailVerified: true,
            IsEligibleRescuer: false,
            ApprovedBy: null,
            ApprovedAt: null);

        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, res.Id);
        Assert.Equal(2, res.RoleId);
        Assert.Equal("new@example.com", res.Email);
        Assert.Equal("lannguyen", res.Username);
        Assert.Equal("Volunteer", res.RescuerType);
        Assert.False(res.IsBanned);
        Assert.Equal(1, uow.SaveCalls);

        // Password is hashed
        var created = repo.CreatedUsers.Single();
        Assert.True(BCrypt.Net.BCrypt.Verify("Str0ngP@ss", created.Password));
    }

    [Fact]
    public async Task AdminCreateUser_DuplicatePhone_ThrowsConflict()
    {
        var existing = BuildUser();
        existing.Phone = "0901234567";
        var repo = new StubUserRepository(existing);
        var uow = new StubUnitOfWork();
        var handler = new AdminCreateUserCommandHandler(repo, uow, NullLogger<AdminCreateUserCommandHandler>.Instance);

        var cmd = new AdminCreateUserCommand(
            Phone: "0901234567", Email: null, FirstName: null, LastName: null,
            Username: null, Password: "P@ss123!", RoleId: 2,
            RescuerType: null, AvatarUrl: null, Address: null, Ward: null, Province: null,
            Latitude: null, Longitude: null, IsEmailVerified: false, IsEligibleRescuer: false,
            ApprovedBy: null, ApprovedAt: null);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task AdminCreateUser_DuplicateEmail_ThrowsConflict()
    {
        var existing = BuildUser();
        existing.Email = "dup@example.com";
        var repo = new StubUserRepository(existing);
        var uow = new StubUnitOfWork();
        var handler = new AdminCreateUserCommandHandler(repo, uow, NullLogger<AdminCreateUserCommandHandler>.Instance);

        var cmd = new AdminCreateUserCommand(
            Phone: null, Email: "dup@example.com", FirstName: null, LastName: null,
            Username: null, Password: "P@ss123!", RoleId: 2,
            RescuerType: null, AvatarUrl: null, Address: null, Ward: null, Province: null,
            Latitude: null, Longitude: null, IsEmailVerified: false, IsEligibleRescuer: false,
            ApprovedBy: null, ApprovedAt: null);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task AdminCreateUser_DuplicateUsername_ThrowsConflict()
    {
        var existing = BuildUser();
        existing.Username = "taken";
        var repo = new StubUserRepository(existing);
        var uow = new StubUnitOfWork();
        var handler = new AdminCreateUserCommandHandler(repo, uow, NullLogger<AdminCreateUserCommandHandler>.Instance);

        var cmd = new AdminCreateUserCommand(
            Phone: null, Email: null, FirstName: null, LastName: null,
            Username: "taken", Password: "P@ss123!", RoleId: 2,
            RescuerType: null, AvatarUrl: null, Address: null, Ward: null, Province: null,
            Latitude: null, Longitude: null, IsEmailVerified: false, IsEligibleRescuer: false,
            ApprovedBy: null, ApprovedAt: null);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task AdminCreateUser_InvalidRescuerType_SetsNull()
    {
        var repo = new StubUserRepository();
        var uow = new StubUnitOfWork();
        var handler = new AdminCreateUserCommandHandler(repo, uow, NullLogger<AdminCreateUserCommandHandler>.Instance);

        var cmd = new AdminCreateUserCommand(
            Phone: "0909999888", Email: null, FirstName: null, LastName: null,
            Username: null, Password: "P@ss123!", RoleId: 2,
            RescuerType: "InvalidType", AvatarUrl: null, Address: null, Ward: null, Province: null,
            Latitude: null, Longitude: null, IsEmailVerified: false, IsEligibleRescuer: false,
            ApprovedBy: null, ApprovedAt: null);

        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.Null(res.RescuerType);
    }

    [Fact]
    public async Task AdminCreateUser_SaveFails_ThrowsCreateFailedException()
    {
        var repo = new StubUserRepository();
        var uow = new FailingSaveUnitOfWork();
        var handler = new AdminCreateUserCommandHandler(repo, uow, NullLogger<AdminCreateUserCommandHandler>.Instance);

        var cmd = new AdminCreateUserCommand(
            Phone: "0901111222", Email: null, FirstName: null, LastName: null,
            Username: null, Password: "P@ss123!", RoleId: 1,
            RescuerType: null, AvatarUrl: null, Address: null, Ward: null, Province: null,
            Latitude: null, Longitude: null, IsEmailVerified: false, IsEligibleRescuer: false,
            ApprovedBy: null, ApprovedAt: null);

        await Assert.ThrowsAsync<CreateFailedException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    // ──────────────────────── AdminUpdateUser ────────────────────────

    [Fact]
    public async Task AdminUpdateUser_Success_UpdatesOnlyProvidedFields()
    {
        var user = BuildUser();
        user.FirstName = "Old";
        user.LastName = "Name";
        user.Phone = "0909999999";
        var repo = new StubUserRepository(user);
        var uow = new StubUnitOfWork();
        var handler = new AdminUpdateUserCommandHandler(repo, uow, NullLogger<AdminUpdateUserCommandHandler>.Instance);

        var cmd = new AdminUpdateUserCommand(
            UserId: user.Id,
            FirstName: "New",
            LastName: null,         // null → keeps "Name"
            Username: null,
            Phone: null,
            Email: null,
            RescuerType: null,
            RoleId: null,
            AvatarUrl: null,
            Address: null,
            Ward: null,
            Province: null,
            Latitude: null,
            Longitude: null,
            IsEmailVerified: null,
            IsEligibleRescuer: null,
            ApprovedBy: null,
            ApprovedAt: null);

        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("New", res.FirstName);
        Assert.Equal("Name", res.LastName); // preserved
        Assert.Equal(1, uow.SaveCalls);
    }

    [Fact]
    public async Task AdminUpdateUser_UserNotFound_ThrowsNotFoundException()
    {
        var repo = new StubUserRepository();
        var uow = new StubUnitOfWork();
        var handler = new AdminUpdateUserCommandHandler(repo, uow, NullLogger<AdminUpdateUserCommandHandler>.Instance);

        var cmd = new AdminUpdateUserCommand(
            UserId: Guid.NewGuid(),
            FirstName: null, LastName: null, Username: null, Phone: null, Email: null,
            RescuerType: null, RoleId: null, AvatarUrl: null, Address: null, Ward: null, Province: null,
            Latitude: null, Longitude: null, IsEmailVerified: null, IsEligibleRescuer: null,
            ApprovedBy: null, ApprovedAt: null);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task AdminUpdateUser_PhoneConflict_ThrowsConflictException()
    {
        var user = BuildUser();
        user.Phone = "0901111111";
        var other = new UserModel { Id = Guid.NewGuid(), Phone = "0902222222", Password = "x" };
        var repo = new StubUserRepository(user, other);
        var uow = new StubUnitOfWork();
        var handler = new AdminUpdateUserCommandHandler(repo, uow, NullLogger<AdminUpdateUserCommandHandler>.Instance);

        var cmd = new AdminUpdateUserCommand(
            UserId: user.Id,
            FirstName: null, LastName: null, Username: null,
            Phone: "0902222222",  // already used by 'other'
            Email: null, RescuerType: null, RoleId: null, AvatarUrl: null,
            Address: null, Ward: null, Province: null,
            Latitude: null, Longitude: null, IsEmailVerified: null, IsEligibleRescuer: null,
            ApprovedBy: null, ApprovedAt: null);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task AdminUpdateUser_SamePhoneAsCurrentUser_NoConflict()
    {
        var user = BuildUser();
        user.Phone = "0901111111";
        var repo = new StubUserRepository(user);
        var uow = new StubUnitOfWork();
        var handler = new AdminUpdateUserCommandHandler(repo, uow, NullLogger<AdminUpdateUserCommandHandler>.Instance);

        var cmd = new AdminUpdateUserCommand(
            UserId: user.Id,
            FirstName: null, LastName: null, Username: null,
            Phone: "0901111111",  // same as current → no conflict check
            Email: null, RescuerType: null, RoleId: null, AvatarUrl: null,
            Address: null, Ward: null, Province: null,
            Latitude: null, Longitude: null, IsEmailVerified: null, IsEligibleRescuer: null,
            ApprovedBy: null, ApprovedAt: null);

        var res = await handler.Handle(cmd, CancellationToken.None);
        Assert.Equal("0901111111", res.Phone);
    }

    [Fact]
    public async Task AdminUpdateUser_HasValueFields_OverwriteWhenProvided()
    {
        var user = BuildUser();
        user.IsEmailVerified = false;
        user.IsEligibleRescuer = false;
        user.RoleId = 2;
        var repo = new StubUserRepository(user);
        var uow = new StubUnitOfWork();
        var handler = new AdminUpdateUserCommandHandler(repo, uow, NullLogger<AdminUpdateUserCommandHandler>.Instance);

        var approvedBy = Guid.NewGuid();
        var approvedAt = DateTime.UtcNow;
        var cmd = new AdminUpdateUserCommand(
            UserId: user.Id,
            FirstName: null, LastName: null, Username: null, Phone: null, Email: null,
            RescuerType: RescuerType.Volunteer,
            RoleId: 3,
            AvatarUrl: null, Address: null, Ward: null, Province: null,
            Latitude: 10.5,
            Longitude: 106.5,
            IsEmailVerified: true,
            IsEligibleRescuer: true,
            ApprovedBy: approvedBy,
            ApprovedAt: approvedAt);

        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(3, res.RoleId);
        Assert.True(res.IsEmailVerified);
        Assert.True(res.IsEligibleRescuer);
        Assert.Equal(approvedBy, res.ApprovedBy);
        Assert.Equal(approvedAt, res.ApprovedAt);
        Assert.Equal("Volunteer", res.RescuerType);
    }

    // ──────────────────────── helpers ────────────────────────

    private static UserModel BuildUser() => new()
    {
        Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        Email = "user@example.com",
        Username = "user@example.com",
        Password = BCrypt.Net.BCrypt.HashPassword("P@ssw0rd!"),
        RoleId = 2,
        IsEmailVerified = true,
    };

    // ──────────────────────── stubs ────────────────────────

    private sealed class StubUserRepository : IUserRepository
    {
        private readonly Dictionary<Guid, UserModel> _usersById = [];
        public UserModel? LastUpdatedUser { get; private set; }
        public List<UserModel> CreatedUsers { get; } = [];

        public StubUserRepository(params UserModel[] seeds)
        {
            foreach (var s in seeds) _usersById[s.Id] = s;
        }

        public Task<UserModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_usersById.GetValueOrDefault(id));
        public Task<UserModel?> GetByUsernameAsync(string u, CancellationToken ct = default)
            => Task.FromResult(_usersById.Values.FirstOrDefault(x =>
                string.Equals(x.Username, u, StringComparison.OrdinalIgnoreCase)));
        public Task<UserModel?> GetByEmailAsync(string e, CancellationToken ct = default)
            => Task.FromResult(_usersById.Values.FirstOrDefault(x =>
                string.Equals(x.Email, e, StringComparison.OrdinalIgnoreCase)));
        public Task<UserModel?> GetByPhoneAsync(string p, CancellationToken ct = default)
            => Task.FromResult(_usersById.Values.FirstOrDefault(x =>
                string.Equals(x.Phone, p, StringComparison.OrdinalIgnoreCase)));
        public Task<List<UserModel>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
            => Task.FromResult(_usersById.Where(kv => ids.Contains(kv.Key)).Select(kv => kv.Value).ToList());
        public Task<UserModel?> GetByEmailVerificationTokenAsync(string t, CancellationToken ct = default)
            => Task.FromResult(_usersById.Values.FirstOrDefault(x => x.EmailVerificationToken == t));
        public Task<UserModel?> GetByPasswordResetTokenAsync(string t, CancellationToken ct = default)
            => Task.FromResult<UserModel?>(null);
        public Task CreateAsync(UserModel user, CancellationToken ct = default)
        { _usersById[user.Id] = user; CreatedUsers.Add(user); return Task.CompletedTask; }
        public Task UpdateAsync(UserModel user, CancellationToken ct = default)
        { LastUpdatedUser = user; _usersById[user.Id] = user; return Task.CompletedTask; }
        public Task<PagedResult<UserModel>> GetPagedAsync(int pn, int ps, int? r = null, bool? b = null, string? s = null, int? er = null, bool? ie = null, RESQ.Domain.Enum.Identity.RescuerType? rt = null, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<PagedResult<UserModel>> GetPagedForPermissionAsync(int pn, int ps, int? r = null, string? n = null, string? p = null, string? e = null, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<List<Guid>> GetActiveCoordinatorUserIdsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<Guid>> GetActiveAdminUserIdsAsync(CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<List<AvailableManagerDto>> GetAvailableManagersAsync(int? excludeDepotId = null, CancellationToken ct = default)
            => throw new NotImplementedException();
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
