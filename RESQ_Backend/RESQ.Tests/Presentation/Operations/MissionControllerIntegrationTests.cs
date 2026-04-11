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
                    CurrentServerStatus = item.TargetStatus
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
                DecisionBy = request.DecisionBy
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