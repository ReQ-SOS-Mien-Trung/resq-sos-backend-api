using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Net.Http.Json;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RESQ.Application.Common.Constants;
using RESQ.Application.Common.Models;
using RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications;
using RESQ.Application.UseCases.Identity.Queries.GetRescuerApplicationStatusMetadata;
using RESQ.Domain.Enum.Identity;

namespace RESQ.Tests.Presentation.Identity;

public class RescuerApplicationAdminControllerIntegrationTests
{
    [Fact]
    public async Task GetRescuerApplications_BindsLowercaseStatusEnum()
    {
        await using var app = await CreateAppAsync();
        var mediator = app.Services.GetRequiredService<TestMediator>();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/identity/admin/rescuer-applications?status=pending&pageNumber=1&pageSize=10");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(mediator.LastQuery);
        Assert.Equal(RescuerApplicationStatus.Pending, mediator.LastQuery!.Status);
    }

    [Fact]
    public async Task GetRescuerApplications_ReturnsBadRequestForInvalidStatus()
    {
        await using var app = await CreateAppAsync();
        var mediator = app.Services.GetRequiredService<TestMediator>();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/identity/admin/rescuer-applications?status=invalid");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(mediator.LastQuery);
    }

    [Fact]
    public async Task GetRescuerApplicationStatuses_ReturnsMetadata()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/identity/admin/rescuer-applications/metadata/statuses");
        var body = await response.Content.ReadFromJsonAsync<List<MetadataDto>>();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(
        [
            ("Pending", "Chờ duyệt"),
            ("Approved", "Đã duyệt"),
            ("Rejected", "Đã từ chối")
        ],
            body!.Select(x => (x.Key, x.Value)).ToList());
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton<TestMediator>();
        builder.Services.AddSingleton<IMediator>(sp => sp.GetRequiredService<TestMediator>());
        builder.Services.AddControllers().AddApplicationPart(typeof(Program).Assembly);
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = TestAuthenticationHandler.Scheme;
            options.DefaultChallengeScheme = TestAuthenticationHandler.Scheme;
            options.DefaultScheme = TestAuthenticationHandler.Scheme;
        }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.Scheme, _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(PermissionConstants.SystemUserView, policy => policy.RequireAuthenticatedUser());
        });

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private sealed class TestMediator : IMediator
    {
        public GetRescuerApplicationsQuery? LastQuery { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            object response = request switch
            {
                GetRescuerApplicationsQuery query => HandleGetRescuerApplications(query),
                GetRescuerApplicationStatusMetadataQuery => new List<MetadataDto>
                {
                    new() { Key = "Pending", Value = "Chờ duyệt" },
                    new() { Key = "Approved", Value = "Đã duyệt" },
                    new() { Key = "Rejected", Value = "Đã từ chối" }
                },
                _ => throw new NotSupportedException($"Unsupported request type: {request.GetType().Name}")
            };

            return Task.FromResult((TResponse)response);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;

        private PagedResult<RescuerApplicationListItemDto> HandleGetRescuerApplications(GetRescuerApplicationsQuery query)
        {
            LastQuery = query;
            return new PagedResult<RescuerApplicationListItemDto>([], 0, query.PageNumber, query.PageSize);
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
