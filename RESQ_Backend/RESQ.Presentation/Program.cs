using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RESQ.Application.Common.Constants;
using RESQ.Application.Extensions;
using RESQ.Infrastructure.Extensions;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Seeding;
using RESQ.Presentation.Authorization;
using RESQ.Presentation.Hubs;
using RESQ.Presentation.Middlewares;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Controllers + JSON enum
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();
builder.Services.AddTransient<GlobalExceptionMiddleware>();

// Add CORS — AllowCredentials is required for SignalR WebSocket handshake
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .SetIsOriginAllowed(_ => true) // allow any origin
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()); // required for SignalR
});

// Add SignalR
builder.Services.AddSignalR();

//jwt swagger
// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Swagger + JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RESQ API",
        Version = "v1",
        Description = "RESQ Backend API with JWT authentication"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Authorization: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Health check
builder.Services.AddHealthChecks();

// Firebase Admin SDK initialization
var firebaseKeyPath = Path.Combine(builder.Environment.ContentRootPath, "PRM PE 142 Firebase Admin SDK.json");
if (FirebaseAdmin.FirebaseApp.DefaultInstance == null)
{
    FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions
    {
        Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromFile(firebaseKeyPath)
    });
}

// Dependency Injection
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();

// ── Memory cache (dùng bởi PermissionAuthorizationHandler) ──────────────
builder.Services.AddMemoryCache();

// ── Dynamic Permission Authorization ────────────────────────────────────
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddAuthorization(options =>
{
    // ── Single-permission policies ──────────────────────────────────────
    void AddSingle(string code) =>
        options.AddPolicy(code, p => p.Requirements.Add(new PermissionRequirement(code)));

    AddSingle(PermissionConstants.SystemConfigManage);
    AddSingle(PermissionConstants.SystemUserManage);
    AddSingle(PermissionConstants.SystemUserView);
    AddSingle(PermissionConstants.InventoryGlobalManage);
    AddSingle(PermissionConstants.InventoryGlobalView);
    AddSingle(PermissionConstants.InventoryDepotManage);
    AddSingle(PermissionConstants.InventoryDepotPointView);
    AddSingle(PermissionConstants.InventorySupplyRequestCreate);
    AddSingle(PermissionConstants.PersonnelDepotBranchManage);
    AddSingle(PermissionConstants.PersonnelGlobalManage);
    AddSingle(PermissionConstants.PersonnelPointManage);
    AddSingle(PermissionConstants.PersonnelTeamView);
    AddSingle(PermissionConstants.PersonnelStatusReport);
    AddSingle(PermissionConstants.MissionGlobalManage);
    AddSingle(PermissionConstants.MissionPointManage);
    AddSingle(PermissionConstants.MissionTeamUpdate);
    AddSingle(PermissionConstants.MissionView);
    AddSingle(PermissionConstants.ActivityGlobalView);
    AddSingle(PermissionConstants.ActivityPointView);
    AddSingle(PermissionConstants.ActivityTeamManage);
    AddSingle(PermissionConstants.ActivityOwnManage);
    AddSingle(PermissionConstants.SosRequestCreate);
    AddSingle(PermissionConstants.SosRequestView);

    // ── Composite / OR-logic policies ──────────────────────────────────
    options.AddPolicy(PermissionConstants.PolicyMissionManage, p => p.Requirements.Add(
        new PermissionRequirement(
            PermissionConstants.MissionGlobalManage,
            PermissionConstants.MissionPointManage)));

    options.AddPolicy(PermissionConstants.PolicyMissionAccess, p => p.Requirements.Add(
        new PermissionRequirement(
            PermissionConstants.MissionGlobalManage,
            PermissionConstants.MissionPointManage,
            PermissionConstants.MissionTeamUpdate,
            PermissionConstants.MissionView)));

    options.AddPolicy(PermissionConstants.PolicyActivityManage, p => p.Requirements.Add(
        new PermissionRequirement(
            PermissionConstants.MissionGlobalManage,
            PermissionConstants.MissionPointManage,
            PermissionConstants.ActivityTeamManage)));

    options.AddPolicy(PermissionConstants.PolicyActivityAccess, p => p.Requirements.Add(
        new PermissionRequirement(
            PermissionConstants.ActivityGlobalView,
            PermissionConstants.ActivityPointView,
            PermissionConstants.MissionGlobalManage,
            PermissionConstants.MissionPointManage,
            PermissionConstants.ActivityTeamManage,
            PermissionConstants.ActivityOwnManage)));

    options.AddPolicy(PermissionConstants.PolicyInventoryRead, p => p.Requirements.Add(
        new PermissionRequirement(
            PermissionConstants.InventoryGlobalManage,
            PermissionConstants.InventoryGlobalView,
            PermissionConstants.InventoryDepotManage,
            PermissionConstants.InventoryDepotPointView)));

    options.AddPolicy(PermissionConstants.PolicyInventoryWrite, p => p.Requirements.Add(
        new PermissionRequirement(
            PermissionConstants.InventoryGlobalManage,
            PermissionConstants.InventoryDepotManage)));

    options.AddPolicy(PermissionConstants.PolicyPersonnelManage, p => p.Requirements.Add(
        new PermissionRequirement(
            PermissionConstants.PersonnelGlobalManage,
            PermissionConstants.PersonnelPointManage)));

    options.AddPolicy(PermissionConstants.PolicyPersonnelAccess, p => p.Requirements.Add(
        new PermissionRequirement(
            PermissionConstants.PersonnelGlobalManage,
            PermissionConstants.PersonnelPointManage,
            PermissionConstants.PersonnelTeamView)));

    options.AddPolicy(PermissionConstants.PolicyDepotView, p => p.Requirements.Add(
        new PermissionRequirement(
            PermissionConstants.InventoryGlobalManage,
            PermissionConstants.InventoryGlobalView,
            PermissionConstants.MissionGlobalManage,
            PermissionConstants.MissionPointManage,
            PermissionConstants.MissionTeamUpdate,
            PermissionConstants.PersonnelGlobalManage,
            PermissionConstants.PersonnelPointManage)));

    options.AddPolicy(PermissionConstants.PolicySosClusterManage, p => p.Requirements.Add(
        new PermissionRequirement(
            PermissionConstants.MissionGlobalManage,
            PermissionConstants.InventoryGlobalManage)));

    options.AddPolicy(PermissionConstants.PolicySosRequestAccess, p => p.Requirements.Add(
        new PermissionRequirement(
            PermissionConstants.SosRequestView,
            PermissionConstants.SosRequestCreate)));

    options.AddPolicy(PermissionConstants.PolicyRouteAccess, p => p.Requirements.Add(
        new PermissionRequirement(
            PermissionConstants.MissionGlobalManage,
            PermissionConstants.MissionPointManage,
            PermissionConstants.MissionTeamUpdate,
            PermissionConstants.ActivityTeamManage,
            PermissionConstants.ActivityOwnManage)));
});


// JWT CONFIGURATION
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("JWT SecretKey is not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],

        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(secretKey)
        ),

        ClockSkew = TimeSpan.Zero
    };

    // Allow SignalR to receive JWT via query string
    // (WebSocket & Server-Sent Events cannot send Authorization header)
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

var app = builder.Build();

// Auto-apply EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
   var db = scope.ServiceProvider.GetRequiredService<ResQDbContext>();
   await db.Database.MigrateAsync();
   // Seed permissions and role-permission mappings (idempotent)
   await PermissionSeeder.SeedAsync(db);
}

// Middleware pipeline

app.UseCors("AllowAll");

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "RESQ API v1");
    c.RoutePrefix = "swagger";
});

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllers();

// 7. Map SignalR Hubs
app.MapHub<ChatHub>("/hubs/chat");

app.Run();
app.Run();
