using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.System;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Identity.Commands.RescuerConsent;
using RESQ.Application.UseCases.Identity.Commands.UpdateRescuerProfile;
using RESQ.Application.UseCases.Identity.Queries.GetCurrentUser;
using RESQ.Domain.Entities.Identity;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Operations;
using RESQ.Domain.Enum.Identity;
using RESQ.Domain.Enum.Logistics;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Identity;

/// <summary>
/// Tests for GetCurrentUser, UpdateRescuerProfile, and RescuerConsent handlers.
/// </summary>
public sealed class UserProfileHandlerTests
{
    // ──────────────────────── UpdateRescuerProfile ────────────────────────

    [Fact]
    public async Task UpdateRescuerProfile_Success_UpdatesAllFieldsAndReturnsResponse()
    {
        var user = BuildUser();
        var repo = new StubUserRepository(user);
        var uow = new StubUnitOfWork();
        var handler = new UpdateRescuerProfileCommandHandler(repo, uow, NullLogger<UpdateRescuerProfileCommandHandler>.Instance);

        var cmd = new UpdateRescuerProfileCommand(
            UserId: user.Id,
            FirstName: "Minh",
            LastName: "Tran",
            Address: "123 Le Loi",
            Ward: "Ben Nghe",
            Province: "HCM",
            Latitude: 10.78,
            Longitude: 106.70,
            AvatarUrl: "https://cdn.example/avatar.png");

        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(user.Id, res.UserId);
        Assert.Equal("Minh", res.FirstName);
        Assert.Equal("Tran", res.LastName);
        Assert.Equal("123 Le Loi", res.Address);
        Assert.Equal("Ben Nghe", res.Ward);
        Assert.Equal("HCM", res.Province);
        Assert.Equal(10.78, res.Latitude);
        Assert.Equal(106.70, res.Longitude);
        Assert.Equal("https://cdn.example/avatar.png", res.AvatarUrl);
        Assert.Same(user, repo.LastUpdatedUser);
        Assert.Equal(1, uow.SaveCalls);
    }

    [Fact]
    public async Task UpdateRescuerProfile_UserNotFound_ThrowsNotFoundException()
    {
        var repo = new StubUserRepository();
        var uow = new StubUnitOfWork();
        var handler = new UpdateRescuerProfileCommandHandler(repo, uow, NullLogger<UpdateRescuerProfileCommandHandler>.Instance);

        var cmd = new UpdateRescuerProfileCommand(
            UserId: Guid.NewGuid(),
            FirstName: "A", LastName: "B",
            Address: null, Ward: null, Province: null,
            Latitude: null, Longitude: null, AvatarUrl: null);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateRescuerProfile_SaveFails_ThrowsCreateFailedException()
    {
        var user = BuildUser();
        var repo = new StubUserRepository(user);
        var uow = new FailingSaveUnitOfWork();
        var handler = new UpdateRescuerProfileCommandHandler(repo, uow, NullLogger<UpdateRescuerProfileCommandHandler>.Instance);

        var cmd = new UpdateRescuerProfileCommand(
            UserId: user.Id,
            FirstName: "A", LastName: "B",
            Address: null, Ward: null, Province: null,
            Latitude: null, Longitude: null, AvatarUrl: null);

        await Assert.ThrowsAsync<CreateFailedException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    // ──────────────────────── RescuerConsent ────────────────────────

    [Fact]
    public async Task RescuerConsent_AllTrue_SetsStep2AndSaves()
    {
        var user = BuildUser();
        user.RescuerStep = 1;
        var repo = new StubUserRepository(user);
        var uow = new StubUnitOfWork();
        var handler = new RescuerConsentCommandHandler(repo, uow, NullLogger<RescuerConsentCommandHandler>.Instance);

        var cmd = new RescuerConsentCommand(
            UserId: user.Id,
            AgreeMedicalFitness: true,
            AgreeLegalResponsibility: true,
            AgreeTraining: true,
            AgreeCodeOfConduct: true);

        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(user.Id, res.UserId);
        // IsEligibleRescuer remains false – only set after admin approval
        Assert.False(res.IsEligibleRescuer);
        Assert.Equal(2, user.RescuerStep);
        Assert.Same(user, repo.LastUpdatedUser);
        Assert.Equal(1, uow.SaveCalls);
    }

    [Fact]
    public async Task RescuerConsent_OneFalse_ReturnsNotEligibleWithoutSaving()
    {
        var user = BuildUser();
        var repo = new StubUserRepository(user);
        var uow = new StubUnitOfWork();
        var handler = new RescuerConsentCommandHandler(repo, uow, NullLogger<RescuerConsentCommandHandler>.Instance);

        var cmd = new RescuerConsentCommand(
            UserId: user.Id,
            AgreeMedicalFitness: true,
            AgreeLegalResponsibility: false,
            AgreeTraining: true,
            AgreeCodeOfConduct: true);

        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(res.IsEligibleRescuer);
        Assert.Equal(0, uow.SaveCalls);
    }

    [Fact]
    public async Task RescuerConsent_UserNotFound_ThrowsNotFoundException()
    {
        var repo = new StubUserRepository();
        var uow = new StubUnitOfWork();
        var handler = new RescuerConsentCommandHandler(repo, uow, NullLogger<RescuerConsentCommandHandler>.Instance);

        var cmd = new RescuerConsentCommand(
            UserId: Guid.NewGuid(),
            AgreeMedicalFitness: true,
            AgreeLegalResponsibility: true,
            AgreeTraining: true,
            AgreeCodeOfConduct: true);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task RescuerConsent_SaveFails_ThrowsCreateFailedException()
    {
        var user = BuildUser();
        var repo = new StubUserRepository(user);
        var uow = new FailingSaveUnitOfWork();
        var handler = new RescuerConsentCommandHandler(repo, uow, NullLogger<RescuerConsentCommandHandler>.Instance);

        var cmd = new RescuerConsentCommand(
            UserId: user.Id,
            AgreeMedicalFitness: true,
            AgreeLegalResponsibility: true,
            AgreeTraining: true,
            AgreeCodeOfConduct: true);

        await Assert.ThrowsAsync<CreateFailedException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    // ──────────────────────── helpers ────────────────────────

    private static UserModel BuildUser() => new()
    {
        Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        Email = "user@example.com",
        Username = "user@example.com",
        Password = BCrypt.Net.BCrypt.HashPassword("P@ssw0rd!"),
        RoleId = 3,
        IsEmailVerified = true,
        RescuerStep = 0
    };

    // ──────────────────────── stubs ────────────────────────

    private sealed class StubUserRepository : IUserRepository
    {
        private readonly Dictionary<Guid, UserModel> _usersById;
        public UserModel? LastUpdatedUser { get; private set; }

        public StubUserRepository(UserModel? seed = null)
        {
            _usersById = seed is null ? [] : new() { [seed.Id] = seed };
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
            => Task.FromResult(_usersById.Values.FirstOrDefault(x => x.EmailVerificationToken == t));
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
