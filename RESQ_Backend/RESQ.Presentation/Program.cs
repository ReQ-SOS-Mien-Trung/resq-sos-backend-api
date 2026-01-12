using RESQ.Application.Common.Interfaces;
using RESQ.Infrastructure.Caching;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Redis Options
var redisOptions = new RedisOptions();
builder.Configuration.GetSection(RedisOptions.SectionName).Bind(redisOptions);
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));

// 2. Register StackExchange.Redis ConnectionMultiplexer as a Singleton
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
    ConnectionMultiplexer.Connect(redisOptions.ConnectionString));

// 3. Register Distributed Redis Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisOptions.ConnectionString;
    options.InstanceName = redisOptions.InstanceName;
});

// 4. Register our custom Cache Service
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// 5. Add Health Checks
builder.Services.AddHealthChecks()
    .AddRedis(redisOptions.ConnectionString, name: "redis");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// Map Health Checks
app.MapHealthChecks("/health");

app.MapControllers();

app.Run();
