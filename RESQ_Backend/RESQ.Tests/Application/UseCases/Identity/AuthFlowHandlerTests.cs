using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Identity.Commands.FirebasePhoneLogin;
using RESQ.Application.UseCases.Identity.Commands.ForgotPassword;
using RESQ.Application.UseCases.Identity.Commands.GoogleLogin;
using RESQ.Application.UseCases.Identity.Commands.Login;
using RESQ.Application.UseCases.Identity.Commands.Logout;
using RESQ.Application.UseCases.Identity.Commands.RegisterRescuer;
using RESQ.Application.UseCases.Identity.Commands.ResendVerificationEmail;
using RESQ.Application.UseCases.Identity.Commands.ResetPassword;
using RESQ.Domain.Entities.Identity;
using RESQ.Domain.Entities.Logistics;
using RESQ.Domain.Entities.Logistics.Models;
using RESQ.Domain.Enum.Logistics;
using RESQ.Application.UseCases.Logistics.Commands.InitiateDepotClosure;
using RESQ.Application.UseCases.Logistics.Queries.GetDepotInventoryByCategory;
using RESQ.Application.UseCases.Logistics.Queries.GetLowStockItems;
using RESQ.Application.UseCases.Logistics.Queries.SearchWarehousesByItems;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Identity;

/// <summary>
/// Tests for Auth flows: Register, Login, Logout, ForgotPassword, ResetPassword,
/// ResendVerificationEmail, GoogleLogin, FirebasePhoneLogin.
/// </summary>
public sealed class AuthFlowHandlerTests
{
    // ──────────────────────── RegisterRescuer ────────────────────────

    [Fact]
    public async Task RegisterRescuer_Success_CreatesUserHashesPasswordAndSendsEmail()
    {
        var repo = new StubUserRepository();
        var uow = new StubUnitOfWork();
        var emailService = new StubEmailService();
        var handler = new RegisterRescuerCommandHandler(repo, uow, emailService, NullLogger<RegisterRescuerCommandHandler>.Instance);

        var cmd = new RegisterRescuerCommand(Email: "new@example.com", Password: "Str0ng!");
        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("new@example.com", res.Email);
        Assert.Equal(3, res.RoleId);
        Assert.False(res.IsEmailVerified);
        Assert.Equal(1, uow.SaveCalls);

        // Password is hashed
        var created = repo.CreatedUsers.Single();
        Assert.True(BCrypt.Net.BCrypt.Verify("Str0ng!", created.Password));

        // Verification email sent
        Assert.Equal("new@example.com", emailService.LastVerificationEmail);
        Assert.NotNull(emailService.LastVerificationToken);
    }

    [Fact]
    public async Task RegisterRescuer_DuplicateEmail_ThrowsConflict()
    {
        var existing = BuildUser();
        existing.Email = "dup@example.com";
        var repo = new StubUserRepository(existing);
        var uow = new StubUnitOfWork();
        var handler = new RegisterRescuerCommandHandler(repo, uow, new StubEmailService(), NullLogger<RegisterRescuerCommandHandler>.Instance);

        var cmd = new RegisterRescuerCommand(Email: "dup@example.com", Password: "P@ss123!");
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterRescuer_SaveFails_ThrowsCreateFailedException()
    {
        var repo = new StubUserRepository();
        var uow = new FailingSaveUnitOfWork();
        var handler = new RegisterRescuerCommandHandler(repo, uow, new StubEmailService(), NullLogger<RegisterRescuerCommandHandler>.Instance);

        var cmd = new RegisterRescuerCommand(Email: "new@example.com", Password: "P@ss123!");
        await Assert.ThrowsAsync<CreateFailedException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    // ──────────────────────── Login ────────────────────────

    [Fact]
    public async Task Login_ByUsername_ReturnsTokensAndPermissions()
    {
        var user = BuildUser();
        user.Username = "admin1";
        user.RoleId = 1;
        var repo = new StubUserRepository(user);
        var permRepo = new StubPermissionRepository(["AdminDashboard"]);
        var tokenService = new StubTokenService(["access-login"], ["refresh-login"]);
        var uow = new StubUnitOfWork();
        var depotInvRepo = new StubDepotInventoryRepository();
        var depotRepo = new StubDepotRepository();
        var config = BuildJwtConfiguration();
        var handler = new LoginCommandHandler(new StubManagerDepotAccessService(), repo, permRepo, tokenService, uow, config,
            NullLogger<LoginCommandHandler>.Instance, depotInvRepo, depotRepo);

        var cmd = new LoginCommand(Username: "admin1", Phone: null, Password: "P@ssw0rd!");
        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("access-login", res.AccessToken);
        Assert.Equal("refresh-login", res.RefreshToken);
        Assert.Equal(user.Id, res.UserId);
        Assert.Equal(["AdminDashboard"], res.Permissions);
    }

    [Fact]
    public async Task Login_ByPhone_ReturnsTokens()
    {
        var user = BuildUser();
        user.Phone = "0901234567";
        var repo = new StubUserRepository(user);
        var permRepo = new StubPermissionRepository([]);
        var tokenService = new StubTokenService(["at"], ["rt"]);
        var uow = new StubUnitOfWork();
        var config = BuildJwtConfiguration();
        var handler = new LoginCommandHandler(new StubManagerDepotAccessService(), repo, permRepo, tokenService, uow, config,
            NullLogger<LoginCommandHandler>.Instance, new StubDepotInventoryRepository(), new StubDepotRepository());

        var cmd = new LoginCommand(Username: null, Phone: "0901234567", Password: "P@ssw0rd!");
        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(user.Id, res.UserId);
    }

    [Fact]
    public async Task Login_NoUsernameOrPhone_ThrowsBadRequest()
    {
        var repo = new StubUserRepository();
        var handler = new LoginCommandHandler(new StubManagerDepotAccessService(), repo, new StubPermissionRepository([]),
            new StubTokenService(), new StubUnitOfWork(), BuildJwtConfiguration(),
            NullLogger<LoginCommandHandler>.Instance, new StubDepotInventoryRepository(), new StubDepotRepository());

        var cmd = new LoginCommand(Username: null, Phone: null, Password: "P@ss");
        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Login_UserNotFound_ThrowsUnauthorized()
    {
        var repo = new StubUserRepository();
        var handler = new LoginCommandHandler(new StubManagerDepotAccessService(), repo, new StubPermissionRepository([]),
            new StubTokenService(), new StubUnitOfWork(), BuildJwtConfiguration(),
            NullLogger<LoginCommandHandler>.Instance, new StubDepotInventoryRepository(), new StubDepotRepository());

        var cmd = new LoginCommand(Username: "nonexistent", Phone: null, Password: "P@ss");
        await Assert.ThrowsAsync<UnauthorizedException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsUnauthorized()
    {
        var user = BuildUser();
        user.Username = "admin1";
        var repo = new StubUserRepository(user);
        var handler = new LoginCommandHandler(new StubManagerDepotAccessService(), repo, new StubPermissionRepository([]),
            new StubTokenService(), new StubUnitOfWork(), BuildJwtConfiguration(),
            NullLogger<LoginCommandHandler>.Instance, new StubDepotInventoryRepository(), new StubDepotRepository());

        var cmd = new LoginCommand(Username: "admin1", Phone: null, Password: "WrongP@ss!");
        await Assert.ThrowsAsync<UnauthorizedException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    // ──────────────────────── Logout ────────────────────────

    [Fact]
    public async Task Logout_ClearsRefreshToken()
    {
        var user = BuildUser();
        user.RefreshToken = "some-token";
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        var repo = new StubUserRepository(user);
        var uow = new StubUnitOfWork();
        var handler = new LogoutCommandHandler(repo, uow, NullLogger<LogoutCommandHandler>.Instance);

        var res = await handler.Handle(new LogoutCommand(user.Id), CancellationToken.None);

        Assert.True(res.Success);
        Assert.Null(user.RefreshToken);
        Assert.Null(user.RefreshTokenExpiry);
    }

    [Fact]
    public async Task Logout_UserNotFound_ThrowsNotFoundException()
    {
        var repo = new StubUserRepository();
        var uow = new StubUnitOfWork();
        var handler = new LogoutCommandHandler(repo, uow, NullLogger<LogoutCommandHandler>.Instance);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new LogoutCommand(Guid.NewGuid()), CancellationToken.None));
    }

    // ──────────────────────── ForgotPassword ────────────────────────

    [Fact]
    public async Task ForgotPassword_UserExists_SetsResetTokenAndSendsEmail()
    {
        var user = BuildUser();
        user.Email = "forgot@example.com";
        var repo = new StubUserRepository(user);
        var uow = new StubUnitOfWork();
        var emailService = new StubEmailService();
        var handler = new ForgotPasswordCommandHandler(repo, uow, emailService, NullLogger<ForgotPasswordCommandHandler>.Instance);

        var res = await handler.Handle(new ForgotPasswordCommand("forgot@example.com"), CancellationToken.None);

        Assert.True(res.Success);
        Assert.NotNull(user.PasswordResetToken);
        Assert.NotNull(user.PasswordResetTokenExpiry);
        Assert.Equal("forgot@example.com", emailService.LastPasswordResetEmail);
    }

    [Fact]
    public async Task ForgotPassword_UserNotFound_StillReturnsSuccess()
    {
        var repo = new StubUserRepository();
        var uow = new StubUnitOfWork();
        var handler = new ForgotPasswordCommandHandler(repo, uow, new StubEmailService(), NullLogger<ForgotPasswordCommandHandler>.Instance);

        var res = await handler.Handle(new ForgotPasswordCommand("nonexistent@example.com"), CancellationToken.None);

        // Anti-enumeration: always returns success
        Assert.True(res.Success);
    }

    // ──────────────────────── ResetPassword ────────────────────────

    [Fact]
    public async Task ResetPassword_ValidToken_HashesNewPasswordAndClearsToken()
    {
        var user = BuildUser();
        user.PasswordResetToken = "valid-token";
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        var repo = new StubUserRepository(user);
        var uow = new StubUnitOfWork();
        var handler = new ResetPasswordCommandHandler(repo, uow, NullLogger<ResetPasswordCommandHandler>.Instance);

        var cmd = new ResetPasswordCommand(Token: "valid-token", NewPassword: "NewP@ss123", ConfirmPassword: "NewP@ss123");
        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(res.Success);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewP@ss123", user.Password));
        Assert.Null(user.PasswordResetToken);
        Assert.Null(user.PasswordResetTokenExpiry);
    }

    [Fact]
    public async Task ResetPassword_MismatchedPasswords_ThrowsBadRequest()
    {
        var repo = new StubUserRepository();
        var uow = new StubUnitOfWork();
        var handler = new ResetPasswordCommandHandler(repo, uow, NullLogger<ResetPasswordCommandHandler>.Instance);

        var cmd = new ResetPasswordCommand(Token: "token", NewPassword: "A", ConfirmPassword: "B");
        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ThrowsBadRequest()
    {
        var repo = new StubUserRepository();
        var uow = new StubUnitOfWork();
        var handler = new ResetPasswordCommandHandler(repo, uow, NullLogger<ResetPasswordCommandHandler>.Instance);

        var cmd = new ResetPasswordCommand(Token: "bad-token", NewPassword: "X", ConfirmPassword: "X");
        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task ResetPassword_ExpiredToken_ThrowsBadRequest()
    {
        var user = BuildUser();
        user.PasswordResetToken = "expired-token";
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(-1); // expired
        var repo = new StubUserRepository(user);
        var uow = new StubUnitOfWork();
        var handler = new ResetPasswordCommandHandler(repo, uow, NullLogger<ResetPasswordCommandHandler>.Instance);

        var cmd = new ResetPasswordCommand(Token: "expired-token", NewPassword: "X", ConfirmPassword: "X");
        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    // ──────────────────────── ResendVerificationEmail ────────────────────────

    [Fact]
    public async Task ResendVerificationEmail_UserExists_Unverified_SendsEmail()
    {
        var user = BuildUser();
        user.Email = "unverified@example.com";
        user.IsEmailVerified = false;
        var repo = new StubUserRepository(user);
        var uow = new StubUnitOfWork();
        var emailService = new StubEmailService();
        var handler = new ResendVerificationEmailCommandHandler(repo, uow, emailService, NullLogger<ResendVerificationEmailCommandHandler>.Instance);

        var cmd = new ResendVerificationEmailCommand("unverified@example.com");
        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(res.Success);
        Assert.NotNull(user.EmailVerificationToken);
        Assert.Equal("unverified@example.com", emailService.LastVerificationEmail);
    }

    [Fact]
    public async Task ResendVerificationEmail_AlreadyVerified_ThrowsBadRequest()
    {
        var user = BuildUser();
        user.Email = "verified@example.com";
        user.IsEmailVerified = true;
        var repo = new StubUserRepository(user);
        var uow = new StubUnitOfWork();
        var handler = new ResendVerificationEmailCommandHandler(repo, uow, new StubEmailService(), NullLogger<ResendVerificationEmailCommandHandler>.Instance);

        var cmd = new ResendVerificationEmailCommand("verified@example.com");
        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task ResendVerificationEmail_UserNotFound_ReturnsSuccess()
    {
        var repo = new StubUserRepository();
        var uow = new StubUnitOfWork();
        var handler = new ResendVerificationEmailCommandHandler(repo, uow, new StubEmailService(), NullLogger<ResendVerificationEmailCommandHandler>.Instance);

        var cmd = new ResendVerificationEmailCommand("nobody@example.com");
        var res = await handler.Handle(cmd, CancellationToken.None);

        // Anti-enumeration: still returns success
        Assert.True(res.Success);
    }

    // ──────────────────────── GoogleLogin ────────────────────────

    [Fact]
    public async Task GoogleLogin_NewUser_CreatesUserAndReturnsTokens()
    {
        var repo = new StubUserRepository();
        var permRepo = new StubPermissionRepository(["MissionView"]);
        var tokenService = new StubTokenService(["g-access"], ["g-refresh"]);
        var uow = new StubUnitOfWork();
        var config = BuildJwtConfiguration();
        var firebaseService = new StubFirebaseService(
            googleResult: new FirebaseGoogleUserInfo
            {
                Uid = "firebase-uid-1",
                Email = "google@example.com",
                GivenName = "Goo",
                FamilyName = "Gle"
            });
        var handler = new GoogleLoginCommandHandler(repo, permRepo, tokenService, uow, config, firebaseService,
            NullLogger<GoogleLoginCommandHandler>.Instance);

        var cmd = new GoogleLoginCommand(IdToken: "firebase-id-token");
        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(res.IsNewUser);
        Assert.Equal("g-access", res.AccessToken);
        Assert.Equal("g-refresh", res.RefreshToken);
        Assert.Equal("Goo", res.FirstName);
        Assert.Equal("Gle", res.LastName);
        Assert.Equal(3, res.RoleId); // DEFAULT_RESCUER_ROLE_ID
        Assert.Equal(["MissionView"], res.Permissions);
    }

    [Fact]
    public async Task GoogleLogin_ExistingUser_DoesNotCreateNew()
    {
        var existing = BuildUser();
        existing.Username = "google@example.com"; // Google uses username = email
        var repo = new StubUserRepository(existing);
        var permRepo = new StubPermissionRepository([]);
        var tokenService = new StubTokenService(["at"], ["rt"]);
        var uow = new StubUnitOfWork();
        var config = BuildJwtConfiguration();
        var firebaseService = new StubFirebaseService(
            googleResult: new FirebaseGoogleUserInfo
            {
                Uid = "uid",
                Email = "google@example.com"
            });
        var handler = new GoogleLoginCommandHandler(repo, permRepo, tokenService, uow, config, firebaseService,
            NullLogger<GoogleLoginCommandHandler>.Instance);

        var cmd = new GoogleLoginCommand(IdToken: "token");
        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(res.IsNewUser);
        Assert.Equal(existing.Id, res.UserId);
    }

    // ──────────────────────── FirebasePhoneLogin ────────────────────────

    [Fact]
    public async Task FirebasePhoneLogin_NewUser_CreatesVictimAndReturnsTokens()
    {
        var repo = new StubUserRepository();
        var permRepo = new StubPermissionRepository(["SosCreate"]);
        var tokenService = new StubTokenService(["p-access"], ["p-refresh"]);
        var uow = new StubUnitOfWork();
        var config = BuildJwtConfiguration();
        var firebaseService = new StubFirebaseService(
            phoneResult: new FirebasePhoneTokenInfo { Uid = "uid-phone", Phone = "0909999888" });
        var handler = new FirebasePhoneLoginCommandHandler(repo, permRepo, firebaseService, tokenService, uow, config,
            NullLogger<FirebasePhoneLoginCommandHandler>.Instance);

        var cmd = new FirebasePhoneLoginCommand(IdToken: "phone-token");
        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(res.IsNewUser);
        Assert.Equal("p-access", res.AccessToken);
        Assert.Equal("0909999888", res.Phone);
        Assert.Equal(5, res.RoleId); // DEFAULT_VICTIM_ROLE_ID
        Assert.Equal(["SosCreate"], res.Permissions);
    }

    [Fact]
    public async Task FirebasePhoneLogin_ExistingUser_DoesNotCreate()
    {
        var existing = BuildUser();
        existing.Phone = "0909999888";
        var repo = new StubUserRepository(existing);
        var permRepo = new StubPermissionRepository([]);
        var tokenService = new StubTokenService(["at"], ["rt"]);
        var uow = new StubUnitOfWork();
        var config = BuildJwtConfiguration();
        var firebaseService = new StubFirebaseService(
            phoneResult: new FirebasePhoneTokenInfo { Uid = "uid", Phone = "0909999888" });
        var handler = new FirebasePhoneLoginCommandHandler(repo, permRepo, firebaseService, tokenService, uow, config,
            NullLogger<FirebasePhoneLoginCommandHandler>.Instance);

        var cmd = new FirebasePhoneLoginCommand(IdToken: "token");
        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(res.IsNewUser);
        Assert.Equal(existing.Id, res.UserId);
    }

    [Fact]
    public async Task FirebasePhoneLogin_NoPhoneInToken_ThrowsBadRequest()
    {
        var repo = new StubUserRepository();
        var firebaseService = new StubFirebaseService(
            phoneResult: new FirebasePhoneTokenInfo { Uid = "uid", Phone = "   " }); // blank
        var handler = new FirebasePhoneLoginCommandHandler(repo, new StubPermissionRepository([]),
            firebaseService, new StubTokenService(), new StubUnitOfWork(), BuildJwtConfiguration(),
            NullLogger<FirebasePhoneLoginCommandHandler>.Instance);

        var cmd = new FirebasePhoneLoginCommand(IdToken: "token");
        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(cmd, CancellationToken.None));
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
    };

    private static IConfiguration BuildJwtConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = "super-secret-key-for-unit-tests-1234567890",
                ["JwtSettings:Issuer"] = "resq-tests",
                ["JwtSettings:Audience"] = "resq-tests-clients",
                ["JwtSettings:AccessTokenExpirationMinutes"] = "60",
                ["JwtSettings:RefreshTokenExpirationDays"] = "7"
            })
            .Build();

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
            => Task.FromResult(_usersById.Values.FirstOrDefault(x => x.PasswordResetToken == t));
        public Task CreateAsync(UserModel user, CancellationToken ct = default)
        { _usersById[user.Id] = user; CreatedUsers.Add(user); return Task.CompletedTask; }
        public Task UpdateAsync(UserModel user, CancellationToken ct = default)
        { LastUpdatedUser = user; _usersById[user.Id] = user; return Task.CompletedTask; }
        public Task<PagedResult<UserModel>> GetPagedAsync(int pn, int ps, int? r = null, bool? b = null, string? s = null, int? er = null, bool? ie = null, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<PagedResult<UserModel>> GetPagedForPermissionAsync(int pn, int ps, int? r = null, string? s = null, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<List<Guid>> GetActiveAdminUserIdsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<AvailableManagerDto>> GetAvailableManagersAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class StubPermissionRepository(List<string>? codes = null) : IPermissionRepository
    {
        private readonly List<string> _codes = codes ?? [];

        public Task<List<string>> GetEffectivePermissionCodesAsync(Guid userId, int? roleId, CancellationToken ct = default)
            => Task.FromResult(_codes.ToList());
        public Task<List<PermissionModel>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<PermissionModel?> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<PermissionModel?> GetByCodeAsync(string code, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CreateAsync(PermissionModel model, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(PermissionModel model, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<PermissionModel>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SetUserPermissionsAsync(Guid userId, Guid grantedBy, List<int> permissionIds, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class StubTokenService(IEnumerable<string>? accessTokens = null, IEnumerable<string>? refreshTokens = null) : ITokenService
    {
        private readonly Queue<string> _access = new(accessTokens ?? ["at"]);
        private readonly Queue<string> _refresh = new(refreshTokens ?? ["rt"]);

        public string GenerateAccessToken(UserModel user) => _access.Count > 0 ? _access.Dequeue() : "at";
        public string GenerateRefreshToken() => _refresh.Count > 0 ? _refresh.Dequeue() : "rt";
        public bool ValidateRefreshToken(string refreshToken) => true;
        public Guid? GetUserIdFromToken(string token) => null;
    }

    private sealed class StubEmailService : IEmailService
    {
        public string? LastVerificationEmail { get; private set; }
        public string? LastVerificationToken { get; private set; }
        public string? LastPasswordResetEmail { get; private set; }

        public Task SendVerificationEmailAsync(string email, string token, CancellationToken ct = default)
        {
            LastVerificationEmail = email;
            LastVerificationToken = token;
            return Task.CompletedTask;
        }

        public Task SendPasswordResetEmailAsync(string email, string resetToken, CancellationToken ct = default)
        {
            LastPasswordResetEmail = email;
            return Task.CompletedTask;
        }

        public Task SendDonationSuccessEmailAsync(string a, string b, decimal c, string d, string e, int f, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendTeamInvitationEmailAsync(string a, string b, string c, int d, Guid e, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubFirebaseService(
        FirebaseGoogleUserInfo? googleResult = null,
        FirebasePhoneTokenInfo? phoneResult = null) : IFirebaseService
    {
        public Task<FirebaseGoogleUserInfo> VerifyGoogleIdTokenAsync(string idToken, CancellationToken ct = default)
            => Task.FromResult(googleResult ?? throw new UnauthorizedException("Invalid token"));
        public Task<FirebasePhoneTokenInfo> VerifyIdTokenAsync(string idToken, CancellationToken ct = default)
            => Task.FromResult(phoneResult ?? throw new UnauthorizedException("Invalid token"));
        public Task SendNotificationToUserAsync(Guid userId, string title, string body, string type = "general", CancellationToken ct = default) => Task.CompletedTask;
        public Task SendNotificationToUserAsync(Guid userId, string title, string body, string type, Dictionary<string, string> data, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendToTopicAsync(string topic, string title, string body, string type = "general", CancellationToken ct = default) => Task.CompletedTask;
        public Task SendToTopicAsync(string topic, string title, string body, Dictionary<string, string> data, CancellationToken ct = default) => Task.CompletedTask;
        public Task SubscribeToUserTopicAsync(string fcmToken, Guid userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task UnsubscribeFromUserTopicAsync(string fcmToken, Guid userId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubDepotInventoryRepository : IDepotInventoryRepository
    {
        public Task<int?> GetActiveDepotIdByManagerAsync(Guid userId, CancellationToken ct = default) => Task.FromResult<int?>(null);
        public Task<List<int>> GetActiveDepotIdsByManagerAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(new List<int>());
        public Task<PagedResult<InventoryItemModel>> GetInventoryPagedAsync(int a, List<int>? b, List<ItemType>? c, List<TargetGroup>? d, string? e, int f, int g, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<PagedResult<InventoryLotModel>> GetInventoryLotsAsync(int a, int b, int c, int d, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<DepotCategoryQuantityDto>> GetInventoryByCategoryAsync(int a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(List<AgentInventoryItem> Items, int TotalCount)> SearchForAgentAsync(string a, string? b, int c, int d, IReadOnlyCollection<int>? e = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(double Latitude, double Longitude)?> GetDepotLocationAsync(int a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(List<WarehouseItemRow> Rows, int TotalItemCount)> SearchWarehousesByItemsAsync(List<int>? a, Dictionary<int, int> b, bool c, int? d, int e, int f, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<SupplyShortageResult>> CheckSupplyAvailabilityAsync(int a, List<(int, string, int)> b, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<MissionSupplyReservationResult> ReserveSuppliesAsync(int a, List<(int, int)> b, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<MissionSupplyPickupExecutionResult> ConsumeReservedSuppliesAsync(int a, List<(int, int)> b, Guid c, int d, int e, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<MissionSupplyReturnExecutionResult> ReceiveMissionReturnAsync(int a, int b, int c, Guid d, List<(int, int)> e, List<(int, string?, string?)> f, List<(int, int)> g, string? h, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<LowStockRawItemDto>> GetLowStockRawItemsAsync(int? a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ReleaseReservedSuppliesAsync(int a, List<(int, int)> b, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ExportInventoryAsync(int a, int b, int c, Guid d, string? e, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AdjustInventoryAsync(int a, int b, int c, Guid d, string e, string? f, DateTime? g, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(int, int?)> BulkTransferForClosureAsync(int a, int b, int c, Guid d, int? e = null, int f = 100, CancellationToken ct = default) => throw new NotImplementedException();
        public Task TransferClosureItemsAsync(int a, int b, int c, int d, Guid e, IReadOnlyCollection<DepotClosureTransferItemMoveDto> f, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Guid?> GetActiveManagerUserIdByDepotIdAsync(int a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ZeroOutForClosureAsync(int a, int b, Guid c, string? d, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> HasActiveInventoryCommitmentsAsync(int a, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class StubDepotRepository(DepotModel? depot = null) : IDepotRepository
    {
        public Task<DepotModel?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult(depot?.Id == id ? depot : null);
        public Task CreateAsync(DepotModel m, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(DepotModel m, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AssignManagerAsync(DepotModel m, Guid? assignedBy = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UnassignManagerAsync(DepotModel m, Guid? unassignedBy = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<PagedResult<DepotModel>> GetAllPagedAsync(int a, int b, IEnumerable<DepotStatus>? c = null, string? d = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IEnumerable<DepotModel>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IEnumerable<DepotModel>> GetAvailableDepotsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<DepotModel?> GetByNameAsync(string n, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> GetActiveDepotCountExcludingAsync(int a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(int, int)> GetNonTerminalSupplyRequestCountsAsync(int a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<decimal> GetConsumableTransferVolumeAsync(int a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(int, int)> GetReusableItemCountsAsync(int a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> GetConsumableInventoryRowCountAsync(int a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<DepotStatus?> GetStatusByIdAsync(int a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> IsManagerActiveElsewhereAsync(Guid a, int b, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ClosureInventoryItemDto>> GetDetailedInventoryForClosureAsync(int a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ClosureInventoryLotItemDto>> GetLotDetailedInventoryForClosureAsync(int a, CancellationToken ct = default) => throw new NotImplementedException();
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

    private sealed class StubManagerDepotAccessService(int? depotId = null)
        : RESQ.Application.Services.IManagerDepotAccessService
    {
        public Task<List<RESQ.Application.Services.ManagedDepotDto>> GetManagedDepotsAsync(
            Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<RESQ.Application.Services.ManagedDepotDto>());

        public Task<int?> ResolveAccessibleDepotIdAsync(
            Guid userId, int? requestedDepotId, CancellationToken cancellationToken = default)
            => Task.FromResult(requestedDepotId ?? depotId);

        public Task EnsureDepotAccessAsync(
            Guid userId, int depotIdParam, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
