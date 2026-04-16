using Microsoft.Extensions.Logging.Abstractions;
using RESQ.Application.Common.Models;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Identity.Commands.RegisterRescuer;
using RESQ.Application.UseCases.Identity.Commands.VerifyEmail;
using RESQ.Domain.Entities.Identity;
using RESQ.Tests.TestDoubles;

namespace RESQ.Tests.Application.UseCases.Identity;

public class RegisterAndVerifyEmailCommandHandlerTests
{
    [Fact]
    public async Task RegisterRescuer_Handle_CreatesUserAndSendsVerificationEmail()
    {
        var userRepository = new StubUserRepository();
        var unitOfWork = new StubUnitOfWork();
        var emailService = new StubEmailService();
        var handler = new RegisterRescuerCommandHandler(
            userRepository,
            unitOfWork,
            emailService,
            NullLogger<RegisterRescuerCommandHandler>.Instance);

        var response = await handler.Handle(
            new RegisterRescuerCommand("rescuer@example.com", "P@ssw0rd!"),
            CancellationToken.None);

        var createdUser = Assert.Single(userRepository.CreatedUsers);
        Assert.Equal(response.UserId, createdUser.Id);
        Assert.Equal("rescuer@example.com", createdUser.Email);
        Assert.Equal("rescuer@example.com", createdUser.Username);
        Assert.Equal(3, createdUser.RoleId);
        Assert.False(createdUser.IsEmailVerified);
        Assert.False(string.IsNullOrWhiteSpace(createdUser.EmailVerificationToken));
        Assert.True(createdUser.EmailVerificationTokenExpiry > DateTime.UtcNow);
        Assert.NotEqual("P@ssw0rd!", createdUser.Password);
        Assert.True(BCrypt.Net.BCrypt.Verify("P@ssw0rd!", createdUser.Password));
        Assert.Equal(1, unitOfWork.SaveCalls);

        Assert.Equal("rescuer@example.com", emailService.LastSentEmail);
        Assert.Equal(createdUser.EmailVerificationToken, emailService.LastVerificationToken);
        Assert.Equal("rescuer@example.com", response.Email);
        Assert.Equal(3, response.RoleId);
        Assert.False(response.IsEmailVerified);
    }

    [Fact]
    public async Task VerifyEmail_Handle_MarksUserAsVerifiedAndClearsToken()
    {
        var existingUser = new UserModel
        {
            Id = Guid.NewGuid(),
            Email = "rescuer@example.com",
            Username = "rescuer@example.com",
            Password = BCrypt.Net.BCrypt.HashPassword("P@ssw0rd!"),
            RoleId = 3,
            IsEmailVerified = false,
            EmailVerificationToken = "verify-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddMinutes(15),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var userRepository = new StubUserRepository(existingUser);
        var unitOfWork = new StubUnitOfWork();
        var handler = new VerifyEmailCommandHandler(
            userRepository,
            unitOfWork,
            NullLogger<VerifyEmailCommandHandler>.Instance);

        var beforeUpdate = existingUser.UpdatedAt;
        var response = await handler.Handle(new VerifyEmailCommand("verify-token"), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("rescuer@example.com", response.Email);
        Assert.True(existingUser.IsEmailVerified);
        Assert.Null(existingUser.EmailVerificationToken);
        Assert.Null(existingUser.EmailVerificationTokenExpiry);
        Assert.True(existingUser.UpdatedAt > beforeUpdate);
        Assert.Same(existingUser, userRepository.LastUpdatedUser);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    private sealed class StubUserRepository(UserModel? seededUser = null) : IUserRepository
    {
        private readonly List<UserModel> _createdUsers = [];
        private readonly Dictionary<Guid, UserModel> _usersById = seededUser is null
            ? []
            : new Dictionary<Guid, UserModel> { [seededUser.Id] = seededUser };

        public IReadOnlyList<UserModel> CreatedUsers => _createdUsers;
        public UserModel? LastUpdatedUser { get; private set; }

        public Task<UserModel?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
            => Task.FromResult(_usersById.Values.FirstOrDefault(user => string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase)));

        public Task<UserModel?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(_usersById.Values.FirstOrDefault(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)));

        public Task<UserModel?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default)
            => Task.FromResult<UserModel?>(null);

        public Task<UserModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_usersById.GetValueOrDefault(id));

        public Task<List<UserModel>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
            => Task.FromResult(_usersById.Where(pair => ids.Contains(pair.Key)).Select(pair => pair.Value).ToList());

        public Task<UserModel?> GetByEmailVerificationTokenAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult(_usersById.Values.FirstOrDefault(user => user.EmailVerificationToken == token));

        public Task<UserModel?> GetByPasswordResetTokenAsync(string token, CancellationToken cancellationToken = default)
            => Task.FromResult<UserModel?>(null);

        public Task CreateAsync(UserModel user, CancellationToken cancellationToken = default)
        {
            _createdUsers.Add(user);
            _usersById[user.Id] = user;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(UserModel user, CancellationToken cancellationToken = default)
        {
            LastUpdatedUser = user;
            _usersById[user.Id] = user;
            return Task.CompletedTask;
        }

        public Task<PagedResult<UserModel>> GetPagedAsync(int pageNumber, int pageSize, int? roleId = null, bool? isBanned = null, string? search = null, int? excludeRoleId = null, bool? isEligible = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PagedResult<UserModel>> GetPagedForPermissionAsync(int pageNumber, int pageSize, int? roleId = null, string? search = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<Guid>> GetActiveCoordinatorUserIdsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Guid>> GetActiveAdminUserIdsAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<AvailableManagerDto>> GetAvailableManagersAsync(int? excludeDepotId = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubEmailService : IEmailService
    {
        public string? LastSentEmail { get; private set; }
        public string? LastVerificationToken { get; private set; }

        public Task SendVerificationEmailAsync(string email, string verificationToken, CancellationToken cancellationToken = default)
        {
            LastSentEmail = email;
            LastVerificationToken = verificationToken;
            return Task.CompletedTask;
        }

        public Task SendPasswordResetEmailAsync(string email, string resetToken, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task SendDonationSuccessEmailAsync(string donorEmail, string donorName, decimal amount, string campaignName, string campaignCode, int donationId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task SendTeamInvitationEmailAsync(string email, string name, string teamName, int teamId, Guid userId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
