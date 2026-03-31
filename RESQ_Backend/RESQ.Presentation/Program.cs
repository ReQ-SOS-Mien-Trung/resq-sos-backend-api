using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RESQ.Application.Extensions;
using RESQ.Presentation.Middlewares;
using RESQ.Application.Services;
using RESQ.Infrastructure.Extensions;
using RESQ.Presentation.Extensions;
using RESQ.Infrastructure.Persistence.Context;
using RESQ.Infrastructure.Persistence.Seeding;
using RESQ.Presentation.Hubs;
using RESQ.Presentation.Middlewares;
using RESQ.Presentation.Services;
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

// Register NotificationHubService (Presentation implementation of Application interface)
builder.Services.AddScoped<INotificationHubService, NotificationHubService>();
builder.Services.AddScoped<IDashboardHubService, DashboardHubService>();

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

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
});

// Health check
builder.Services.AddHealthChecks();

// Firebase Admin SDK initialization
// Supports three configuration methods (in priority order):
//   1. FIREBASE_CREDENTIALS_JSON env var — raw JSON string (best for Docker/CI)
//   2. FIREBASE_CREDENTIALS_JSON_BASE64 env var — base64-encoded JSON (avoids quoting issues)
//   3. File on disk at ContentRootPath (legacy / local dev)
if (FirebaseAdmin.FirebaseApp.DefaultInstance == null)
{
    Google.Apis.Auth.OAuth2.GoogleCredential firebaseCredential;

    var firebaseJson = builder.Configuration["FIREBASE_CREDENTIALS_JSON"]
                       ?? Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_JSON");
    var firebaseJsonBase64 = builder.Configuration["FIREBASE_CREDENTIALS_JSON_BASE64"]
                             ?? Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_JSON_BASE64");

    if (!string.IsNullOrWhiteSpace(firebaseJson))
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(firebaseJson));
        firebaseCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromStream(stream);
    }
    else if (!string.IsNullOrWhiteSpace(firebaseJsonBase64))
    {
        var jsonBytes = Convert.FromBase64String(firebaseJsonBase64);
        using var stream = new MemoryStream(jsonBytes);
        firebaseCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromStream(stream);
    }
    else
    {
        var firebaseKeyPath = Path.Combine(builder.Environment.ContentRootPath, "PRM PE 142 Firebase Admin SDK.json");
        if (!File.Exists(firebaseKeyPath))
            throw new FileNotFoundException(
                $"Firebase credential file is missing. Either set the FIREBASE_CREDENTIALS_JSON environment variable, " +
                $"or mount a valid JSON file to '{firebaseKeyPath}'.",
                firebaseKeyPath);
        firebaseCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromFile(firebaseKeyPath);
    }

    FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions
    {
        Credential = firebaseCredential
    });
}

// Dependency Injection
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();

// ── Memory cache (dùng bởi PermissionAuthorizationHandler) ──────────────
builder.Services.AddMemoryCache();

// ── Dynamic Permission Authorization ────────────────────────────────────
builder.Services.AddPermissionAuthorization();


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

// Ensure database schema is applied when the API starts in Docker/production.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ResQDbContext>();
    dbContext.Database.Migrate();

    dbContext.Database.ExecuteSqlRaw("""
        CREATE SEQUENCE IF NOT EXISTS depot_realtime_version_seq;

        CREATE TABLE IF NOT EXISTS depot_realtime_outbox (
            id uuid PRIMARY KEY,
            depot_id integer NOT NULL,
            mission_id integer NULL,
            version bigint NOT NULL DEFAULT nextval('depot_realtime_version_seq'),
            event_type varchar(120) NOT NULL,
            operation varchar(40) NOT NULL,
            payload_kind varchar(20) NOT NULL,
            is_critical boolean NOT NULL,
            changed_fields text NULL,
            snapshot_payload text NULL,
            status varchar(20) NOT NULL DEFAULT 'Pending',
            attempt_count integer NOT NULL DEFAULT 0,
            next_attempt_at timestamp with time zone NOT NULL,
            occurred_at timestamp with time zone NOT NULL,
            lock_owner varchar(120) NULL,
            lock_expires_at timestamp with time zone NULL,
            last_error text NULL,
            processed_at timestamp with time zone NULL,
            created_at timestamp with time zone NOT NULL DEFAULT now(),
            updated_at timestamp with time zone NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS ix_depot_realtime_outbox_depot_version
            ON depot_realtime_outbox (depot_id, version);

        CREATE INDEX IF NOT EXISTS ix_depot_realtime_outbox_status_next_attempt
            ON depot_realtime_outbox (status, next_attempt_at);

        CREATE TABLE IF NOT EXISTS supply_request_priority_configs (
            id integer PRIMARY KEY,
            urgent_minutes integer NOT NULL,
            high_minutes integer NOT NULL,
            medium_minutes integer NOT NULL,
            updated_by uuid NULL,
            updated_at timestamp with time zone NOT NULL DEFAULT now(),
            CONSTRAINT ck_supply_request_priority_configs_order
                CHECK (urgent_minutes > 0 AND urgent_minutes < high_minutes AND high_minutes < medium_minutes)
        );

        INSERT INTO supply_request_priority_configs (id, urgent_minutes, high_minutes, medium_minutes, updated_by, updated_at)
        VALUES (1, 10, 20, 30, NULL, now())
        ON CONFLICT (id) DO NOTHING;

        ALTER TABLE depot_supply_requests
            ADD COLUMN IF NOT EXISTS priority_level varchar(20) NOT NULL DEFAULT 'Medium';

        ALTER TABLE depot_supply_requests
            ADD COLUMN IF NOT EXISTS auto_reject_at timestamp with time zone NULL;

        ALTER TABLE depot_supply_requests
            ADD COLUMN IF NOT EXISTS high_escalation_notified boolean NOT NULL DEFAULT false;

        ALTER TABLE depot_supply_requests
            ADD COLUMN IF NOT EXISTS high_escalation_notified_at timestamp with time zone NULL;

        ALTER TABLE depot_supply_requests
            ADD COLUMN IF NOT EXISTS urgent_escalation_notified boolean NOT NULL DEFAULT false;

        ALTER TABLE depot_supply_requests
            ADD COLUMN IF NOT EXISTS urgent_escalation_notified_at timestamp with time zone NULL;

        UPDATE depot_supply_requests
        SET auto_reject_at = CASE priority_level
            WHEN 'Urgent' THEN created_at + INTERVAL '10 minute'
            WHEN 'High' THEN created_at + INTERVAL '20 minute'
            ELSE created_at + INTERVAL '30 minute'
        END
        WHERE auto_reject_at IS NULL
          AND source_status = 'Pending'
          AND requesting_status = 'WaitingForApproval';
        """);
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
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<DashboardHub>("/hubs/dashboard");

app.Run();
