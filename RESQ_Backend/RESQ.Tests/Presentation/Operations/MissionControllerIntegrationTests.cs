using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RESQ.Application.Common.Constants;
using RESQ.Application.Exceptions;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Operations.Commands.SyncMissionActivities;
using RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;
using RESQ.Application.UseCases.Operations.Commands.UpdateMissionStatus;
using RESQ.Infrastructure.Services;
using RESQ.Presentation.Authorization;
using RESQ.Presentation.Middlewares;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Tests.Presentation.Operations;

public class MissionControllerIntegrationTests
{
    [Fact]
    public async Task SyncMissionActivities_ReturnsForbidden_WhenUserLacksRequiredPermission()
    {
        await using var factory = new MissionApiFactory();
        factory.State.SetPermissions();

        using var client = factory.CreateHttpsClient();
        var response = await client.PostAsJsonAsync(
            "/operations/missions/activities/sync/my-team",
            new SyncMissionActivitiesRequestDto
            {
                Items =
                [
                    CreateSyncItem()
                ]
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(factory.State.LastSyncCommand);
    }

    [Fact]
    public async Task SyncMissionActivities_ReturnsBadRequest_WhenRequestFailsValidation()
    {
        await using var factory = new MissionApiFactory();
        factory.State.SetPermissions(PermissionConstants.ActivityOwnManage);

        using var client = factory.CreateHttpsClient();
        var response = await client.PostAsJsonAsync(
            "/operations/missions/activities/sync/my-team",
            new SyncMissionActivitiesRequestDto());

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(error);
        Assert.Equal("Lỗi xác thực dữ liệu", error!.Message);
        Assert.Null(factory.State.LastSyncCommand);
    }

    [Fact]
    public async Task UpdateActivityStatus_ReturnsBadRequest_WhenMissionActivityMismatchIsRaised()
    {
        await using var factory = new MissionApiFactory();
        factory.State.SetPermissions(PermissionConstants.ActivityOwnManage);
        factory.State.UpdateActivityStatusException = new BadRequestException("Activity này không thuộc mission được chỉ định.");

        using var client = factory.CreateHttpsClient();
        var response = await client.PatchAsJsonAsync(
            "/operations/missions/999/activities/111/status",
            new { status = "Succeed" });

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(error);
        Assert.Equal("Activity này không thuộc mission được chỉ định.", error!.Message);
        Assert.NotNull(factory.State.LastUpdateActivityStatusCommand);
        Assert.Equal(999, factory.State.LastUpdateActivityStatusCommand!.MissionId);
        Assert.Equal(111, factory.State.LastUpdateActivityStatusCommand.ActivityId);
    }

    [Fact]
    public async Task UpdateActivityStatus_AcceptsPendingConfirmationAndForwardsCommand()
    {
        await using var factory = new MissionApiFactory();
        factory.State.SetPermissions(PermissionConstants.ActivityOwnManage);

        using var client = factory.CreateHttpsClient();
        var response = await client.PatchAsJsonAsync(
            "/operations/missions/77/activities/88/status",
            new { status = "pendingconfirmation" });

        var body = await response.Content.ReadFromJsonAsync<UpdateActivityStatusResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(factory.State.LastUpdateActivityStatusCommand);
        Assert.Equal(77, factory.State.LastUpdateActivityStatusCommand!.MissionId);
        Assert.Equal(88, factory.State.LastUpdateActivityStatusCommand.ActivityId);
        Assert.Equal(MissionActivityStatus.PendingConfirmation, factory.State.LastUpdateActivityStatusCommand.Status);
        Assert.Equal(Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"), factory.State.LastUpdateActivityStatusCommand.DecisionBy);
        Assert.NotNull(body);
        Assert.Equal("PendingConfirmation", body!.Status);
    }

    [Fact]
    public async Task UpdateActivityStatus_AcceptsImageUrlAndForwardsCommand()
    {
        const string imageUrl = "https://cdn.example.com/activity-proof.jpg";

        await using var factory = new MissionApiFactory();
        factory.State.SetPermissions(PermissionConstants.ActivityOwnManage);

        using var client = factory.CreateHttpsClient();
        var response = await client.PatchAsJsonAsync(
            "/operations/missions/77/activities/88/status",
            new { status = "Succeed", imageUrl });

        var body = await response.Content.ReadFromJsonAsync<UpdateActivityStatusResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(factory.State.LastUpdateActivityStatusCommand);
        Assert.Equal(imageUrl, factory.State.LastUpdateActivityStatusCommand!.ImageUrl);
        Assert.NotNull(body);
        Assert.Equal(imageUrl, body!.ImageUrl);
    }

    [Fact]
    public async Task UpdateActivityStatus_ReturnsBadRequest_WhenStatusIsNotDefined()
    {
        await using var factory = new MissionApiFactory();
        factory.State.SetPermissions(PermissionConstants.ActivityOwnManage);

        using var client = factory.CreateHttpsClient();
        var response = await client.PatchAsJsonAsync(
            "/operations/missions/77/activities/88/status",
            new { status = "999" });

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(error);
        Assert.Contains("999", error!.Message, StringComparison.Ordinal);
        Assert.Null(factory.State.LastUpdateActivityStatusCommand);
    }

    [Fact]
    public async Task UpdateMissionStatus_ForwardsOnGoingCommand_WhenPermissionAllows()
    {
        await using var factory = new MissionApiFactory();
        factory.State.SetPermissions(PermissionConstants.ActivityTeamManage);

        using var client = factory.CreateHttpsClient();
        var response = await client.PatchAsJsonAsync(
            "/operations/missions/321/status",
            new { status = "OnGoing" });

        var body = await response.Content.ReadFromJsonAsync<UpdateMissionStatusResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(factory.State.LastUpdateMissionStatusCommand);
        Assert.Equal(321, factory.State.LastUpdateMissionStatusCommand!.MissionId);
        Assert.Equal(MissionStatus.OnGoing, factory.State.LastUpdateMissionStatusCommand.Status);
        Assert.Equal(Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"), factory.State.LastUpdateMissionStatusCommand.DecisionBy);
        Assert.NotNull(body);
        Assert.Equal(321, body!.MissionId);
        Assert.Equal("OnGoing", body.Status);
        Assert.False(body.IsCompleted);
    }

    private static MissionActivitySyncItemDto CreateSyncItem() => new()
    {
        ClientMutationId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
        MissionId = 12,
        ActivityId = 34,
        TargetStatus = MissionActivityStatus.Succeed,
        BaseServerStatus = MissionActivityStatus.OnGoing,
        QueuedAt = new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero)
    };

    private sealed class MissionApiFactory : WebApplicationFactory<Program>
    {
        public MissionApiFactoryState State { get; } = new();

        public HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Testing:SkipFirebaseInitialization"] = "true",
                    ["Testing:SkipDatabaseMigration"] = "true",
                    ["ConnectionStrings:ResQDb"] = "Host=localhost;Database=resq_test;Username=test;Password=test",
                    ["JwtSettings:SecretKey"] = "test-secret-key-for-integration-tests-1234567890",
                    ["JwtSettings:Issuer"] = "resq-test",
                    ["JwtSettings:Audience"] = "resq-test"
                });
            });

            builder.ConfigureServices(services =>
            {
                RemoveHostedServices(services);

                services.RemoveAll<IUserPermissionResolver>();
                services.AddScoped<IUserPermissionResolver>(_ => new TestUserPermissionResolver(State));

                services.RemoveAll(typeof(IRequestHandler<SyncMissionActivitiesCommand, SyncMissionActivitiesResponseDto>));
                services.AddScoped<IRequestHandler<SyncMissionActivitiesCommand, SyncMissionActivitiesResponseDto>>(_ =>
                    new StubSyncMissionActivitiesHandler(State));

                services.RemoveAll(typeof(IRequestHandler<UpdateActivityStatusCommand, UpdateActivityStatusResponse>));
                services.AddScoped<IRequestHandler<UpdateActivityStatusCommand, UpdateActivityStatusResponse>>(_ =>
                    new StubUpdateActivityStatusHandler(State));

                services.RemoveAll(typeof(IRequestHandler<UpdateMissionStatusCommand, UpdateMissionStatusResponse>));
                services.AddScoped<IRequestHandler<UpdateMissionStatusCommand, UpdateMissionStatusResponse>>(_ =>
                    new StubUpdateMissionStatusHandler(State));

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.Scheme;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.Scheme;
                    options.DefaultScheme = TestAuthenticationHandler.Scheme;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.Scheme,
                    _ => { });
            });
        }

        private static void RemoveHostedServices(IServiceCollection services)
        {
            var hostedServices = services
                .Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType?.Namespace?.StartsWith("RESQ.Infrastructure.Services", StringComparison.Ordinal) == true)
                .ToList();

            foreach (var descriptor in hostedServices)
            {
                services.Remove(descriptor);
            }
        }
    }

    private sealed class MissionApiFactoryState
    {
        public HashSet<string> Permissions { get; } = new(StringComparer.OrdinalIgnoreCase);

        public SyncMissionActivitiesCommand? LastSyncCommand { get; set; }

        public UpdateActivityStatusCommand? LastUpdateActivityStatusCommand { get; set; }

        public UpdateMissionStatusCommand? LastUpdateMissionStatusCommand { get; set; }

        public Exception? UpdateActivityStatusException { get; set; }

        public void SetPermissions(params string[] permissions)
        {
            Permissions.Clear();
            foreach (var permission in permissions)
            {
                Permissions.Add(permission);
            }

            LastSyncCommand = null;
            LastUpdateActivityStatusCommand = null;
            LastUpdateMissionStatusCommand = null;
            UpdateActivityStatusException = null;
        }
    }

    private sealed class TestUserPermissionResolver(MissionApiFactoryState state) : IUserPermissionResolver
    {
        private readonly MissionApiFactoryState _state = state;

        public Task<IReadOnlyCollection<string>> GetEffectivePermissionCodesAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<string>>(_state.Permissions.ToArray());
    }

    private sealed class StubSyncMissionActivitiesHandler(MissionApiFactoryState state)
        : IRequestHandler<SyncMissionActivitiesCommand, SyncMissionActivitiesResponseDto>
    {
        private readonly MissionApiFactoryState _state = state;

        public Task<SyncMissionActivitiesResponseDto> Handle(SyncMissionActivitiesCommand request, CancellationToken cancellationToken)
        {
            _state.LastSyncCommand = request;

            return Task.FromResult(new SyncMissionActivitiesResponseDto
            {
                Summary = new MissionActivitySyncSummaryDto
                {
                    Total = request.Items.Count,
                    Applied = request.Items.Count
                },
                Results = request.Items.Select(item => new MissionActivitySyncResultDto
                {
                    ClientMutationId = item.ClientMutationId,
                    MissionId = item.MissionId,
                    ActivityId = item.ActivityId,
                    TargetStatus = item.TargetStatus,
                    BaseServerStatus = item.BaseServerStatus,
                    Outcome = "applied",
                    EffectiveStatus = item.TargetStatus,
                    CurrentServerStatus = item.TargetStatus,
                    ImageUrl = item.ImageUrl
                }).ToList()
            });
        }
    }

    private sealed class StubUpdateActivityStatusHandler(MissionApiFactoryState state)
        : IRequestHandler<UpdateActivityStatusCommand, UpdateActivityStatusResponse>
    {
        private readonly MissionApiFactoryState _state = state;

        public Task<UpdateActivityStatusResponse> Handle(UpdateActivityStatusCommand request, CancellationToken cancellationToken)
        {
            _state.LastUpdateActivityStatusCommand = request;

            if (_state.UpdateActivityStatusException is not null)
            {
                throw _state.UpdateActivityStatusException;
            }

            return Task.FromResult(new UpdateActivityStatusResponse
            {
                ActivityId = request.ActivityId,
                Status = request.Status.ToString(),
                DecisionBy = request.DecisionBy,
                ImageUrl = request.ImageUrl
            });
        }
    }

    private sealed class StubUpdateMissionStatusHandler(MissionApiFactoryState state)
        : IRequestHandler<UpdateMissionStatusCommand, UpdateMissionStatusResponse>
    {
        private readonly MissionApiFactoryState _state = state;

        public Task<UpdateMissionStatusResponse> Handle(UpdateMissionStatusCommand request, CancellationToken cancellationToken)
        {
            _state.LastUpdateMissionStatusCommand = request;

            return Task.FromResult(new UpdateMissionStatusResponse
            {
                MissionId = request.MissionId,
                Status = request.Status.ToString(),
                IsCompleted = request.Status is MissionStatus.Completed or MissionStatus.Incompleted
            });
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "aaaaaaaa-1111-1111-1111-111111111111"),
                new Claim(ClaimTypes.Name, "integration-test-user")
            };

            var identity = new ClaimsIdentity(claims, Scheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
