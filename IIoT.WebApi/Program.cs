using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.AspNetCore;
using IIoT.Shared.Configuration;
using IIoT.Shared.Models;
using IIoT.WebApi.Core.Interfaces;
using IIoT.WebApi.Data.Repositories;
using IIoT.WebApi.Data.TypeHandlers;
using IIoT.WebApi.Hubs;
using IIoT.WebApi.Services;
using Serilog;

// Локальный запуск: подтягиваем .env до создания билдера (в Docker строка приходит из ConnectionStrings__*)
LocalEnv.Load();

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrEmpty(builder.Configuration.GetConnectionString("AdamMonitoring")))
    builder.Configuration["ConnectionStrings:AdamMonitoring"] = LocalEnv.BuildPostgresConnectionString();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
    });

// Configure Routing to use lowercase URLs
builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
});

// Configure FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register DapperContext as a singleton for database connectivity and type mapping
builder.Services.AddSingleton<DapperContext>();

// Register Repositories
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddScoped<IConnectionRepository, ConnectionRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();
builder.Services.AddScoped<IMetricsRepository, MetricsRepository>();
builder.Services.AddScoped<ISystemRepository, SystemRepository>();
builder.Services.AddScoped<ICommandRepository, CommandRepository>();

// Enable SignalR for real-time communication
builder.Services.AddSignalR();

// Register the background service that listens for PostgreSQL NOTIFY events
builder.Services.AddHostedService<MetricsObserverService>();
builder.Services.AddHostedService<SystemHealthService>();
builder.Services.AddHostedService<CommandObserverService>();

// Add CORS policy
var defaultOrigins = new[]
{
    "http://localhost:3000",
    "https://localhost:3000",
    "http://localhost:5173",
    "https://localhost:5173"
};

var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (configuredOrigins == null || configuredOrigins.Length == 0)
{
    var rawOrigins = builder.Configuration["Cors:AllowedOrigins"];
    if (!string.IsNullOrWhiteSpace(rawOrigins))
    {
        configuredOrigins = rawOrigins.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

var allowedOrigins = (configuredOrigins != null && configuredOrigins.Length > 0)
    ? configuredOrigins
    : defaultOrigins;

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

// app.UseHttpsRedirection(); // Disabled for Docker/Cloudflare stability

app.UseMiddleware<IIoT.WebApi.Middleware.GlobalExceptionMiddleware>();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

// Map SignalR hubs
app.MapHub<MonitoringHub>("/hubs/metrics");
app.MapHub<ControlHub>("/hubs/control");

app.Run();
