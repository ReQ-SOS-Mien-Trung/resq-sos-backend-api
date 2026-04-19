using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Identity.Commands.BanUser;
using RESQ.Application.UseCases.Identity.Commands.UnbanUser;
using RESQ.Domain.Entities.Identity;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Identity;

/// <summary>
/// Tests for BanUser and UnbanUser handlers.
/// </summary>
public sealed class BanUnbanUserHandlerTests
{
    // ──────────────────────── BanUser ────────────────────────

    [Fact]
    public async Task BanUser_Success_SetsBanFieldsAndClearsRefreshToken()
    {
        var user = BuildUser();
        user.RefreshToken = "some-refresh-token";
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        var repo = new StubUserRepository(user);
        var uow = new StubUnitOfWork();
        var handler = new BanUserCommandHandler(repo, uow, NullLogger<BanUserCommandHandler>.Instance);

        var adminId = Guid.NewGuid();
        var cmd = new BanUserCommand(TargetUserId: user.Id, AdminId: adminId, Reason: "Spam");

        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(res.IsBanned);
        Assert.Equal(adminId, res.BannedBy);
        Assert.Equal("Spam", res.BanReason);
        Assert.NotNull(res.BannedAt);
        // Refresh token must be cleared
        Assert.Null(user.RefreshToken);
        Assert.Null(user.RefreshTokenExpiry);
        Assert.Same(user, repo.LastUpdatedUser);
        Assert.Equal(1, uow.SaveCalls);
    }

    [Fact]
    public async Task BanUser_UserNotFound_ThrowsNotFoundException()
    {
        var repo = new StubUserRepository();
        var uow = new StubUnitOfWork();
        var handler = new BanUserCommandHandler(repo, uow, NullLogger<BanUserCommandHandler>.Instance);

        var cmd = new BanUserCommand(TargetUserId: Guid.NewGuid(), AdminId: Guid.NewGuid(), Reason: "test");

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task BanUser_AlreadyBanned_ThrowsConflictException()
    {
        var user = BuildUser();
        user.IsBanned = true;
        var repo = new StubUserRepository(user);
        var uow = new StubUnitOfWork();
        var handler = new BanUserCommandHandler(repo, uow, NullLogger<BanUserCommandHandler>.Instance);

        var cmd = new BanUserCommand(TargetUserId: user.Id, AdminId: Guid.NewGuid(), Reason: "double ban");

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    // ──────────────────────── UnbanUser ────────────────────────

    [Fact]
    public async Task UnbanUser_Success_ClearsBanFields()
    {
        var user = BuildUser();
        user.IsBanned = true;
        user.BannedBy = Guid.NewGuid();
        user.BannedAt = DateTime.UtcNow.AddDays(-1);
        user.BanReason = "Old reason";
        var repo = new StubUserRepository(user);
        var uow = new StubUnitOfWork();
        var handler = new UnbanUserCommandHandler(repo, uow, NullLogger<UnbanUserCommandHandler>.Instance);

        var cmd = new UnbanUserCommand(TargetUserId: user.Id);

        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(res.IsBanned);
        Assert.False(user.IsBanned);
        Assert.Null(user.BannedBy);
        Assert.Null(user.BannedAt);
        Assert.Null(user.BanReason);
        Assert.Equal(1, uow.SaveCalls);
    }

    [Fact]
    public async Task UnbanUser_UserNotFound_ThrowsNotFoundException()
    {
        var repo = new StubUserRepository();
        var uow = new StubUnitOfWork();
        var handler = new UnbanUserCommandHandler(repo, uow, NullLogger<UnbanUserCommandHandler>.Instance);

        var cmd = new UnbanUserCommand(TargetUserId: Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task UnbanUser_NotBanned_ThrowsConflictException()
    {
        var user = BuildUser();
        user.IsBanned = false;
        var repo = new StubUserRepository(user);
        var uow = new StubUnitOfWork();
        var handler = new UnbanUserCommandHandler(repo, uow, NullLogger<UnbanUserCommandHandler>.Instance);

        var cmd = new UnbanUserCommand(TargetUserId: user.Id);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    // ──────────────────────── helpers ────────────────────────

    private static UserModel BuildUser() => new()
    {
        Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        Email = "user@example.com",
        Username = "user@example.com",
        Password = BCrypt.Net.BCrypt.HashPassword("P@ssw0rd!"),
        RoleId = 2,
        IsBanned = false,
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
        public Task<PagedResult<UserModel>> GetPagedAsync(int pn, int ps, int? r = null, bool? b = null, string? s = null, int? er = null, bool? ie = null, RESQ.Domain.Enum.Identity.RescuerType? rt = null, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<PagedResult<UserModel>> GetPagedForPermissionAsync(int pn, int ps, int? r = null, string? n = null, string? p = null, string? e = null, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<List<Guid>> GetActiveAdminUserIdsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<Guid>> GetActiveCoordinatorUserIdsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<AvailableManagerDto>> GetAvailableManagersAsync(int? excludeDepotId = null, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
