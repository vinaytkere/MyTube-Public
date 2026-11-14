using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using MyTube.Backend.Application;
using MyTube.Backend.Application.Security;
using MyTube.Backend.Infrastructure;
using Scalar.AspNetCore;

using Microsoft.AspNetCore.DataProtection;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Application (CQRS, MediatR, Validation) & Infrastructure Services
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// 2. Add API controllers with String Enum Converter
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// 3. Add OpenAPI metadata (native .NET 10)
builder.Services.AddOpenApi();

// 3a. Standardized RFC 7807 ProblemDetails & Exception Handler
builder.Services.AddExceptionHandler<MyTube.Backend.Api.Middleware.ApiExceptionHandler>();
builder.Services.AddProblemDetails();

// 3b. Standardized ASP.NET Core Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<VideoDbContext>("database");

// 4. Configure CORS with strict allowed origins whitelist
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
                     ?? [ "http://localhost:5003", "http://localhost:3000", "http://localhost:5173" ];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 4a. Persist ASP.NET Core Data Protection keys across container restarts
var dataDir = builder.Configuration["DATA_DIR"] ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
var keysDir = Path.Combine(dataDir, "keys");
Directory.CreateDirectory(keysDir);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
    .SetApplicationName("MyTube");

// 5. Configure JWT Authentication using SecretManagementService
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<ISecretManagementService>((options, secretService) =>
    {
        var secretKey = secretService.GetSecret("Authentication:Jwt:Key");
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
        {
            throw new InvalidOperationException("Required security secret 'Authentication:Jwt:Key' is missing or too short (minimum 32 characters). Configure it via /run/secrets/jwt_secret or JWT_SECRET.");
        }
        var issuer = builder.Configuration["Authentication:Jwt:Issuer"] ?? "MyTube";
        var audience = builder.Configuration["Authentication:Jwt:Audience"] ?? "MyTubeUsers";

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10 MB limit for live audio chunk streaming
    options.EnableDetailedErrors = true;
}).AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddSingleton<IDownloadNotifier, MyTube.Backend.Api.Hubs.SignalRDownloadNotifier>();


builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Standardized RFC 7807 global exception handling
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseForwardedHeaders();

// Bulletproof CORS middleware guaranteeing Access-Control-Allow-Origin & SignalR headers on all endpoints, range streams, and OPTIONS preflights
app.Use(async (context, next) =>
{
    var origin = context.Request.Headers["Origin"].ToString();
    var allowedOrigin = !string.IsNullOrWhiteSpace(origin) ? origin : "*";

    context.Response.OnStarting(() =>
    {
        if (!context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = allowedOrigin;
        }
        if (allowedOrigin != "*")
        {
            context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
        }
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS, HEAD";
        context.Response.Headers["Access-Control-Expose-Headers"] = "Content-Length, Content-Range, Accept-Ranges, Content-Type";

        var reqHeaders = context.Request.Headers["Access-Control-Request-Headers"].ToString();
        if (!string.IsNullOrWhiteSpace(reqHeaders))
        {
            context.Response.Headers["Access-Control-Allow-Headers"] = reqHeaders;
        }
        else
        {
            context.Response.Headers["Access-Control-Allow-Headers"] = "x-requested-with, x-signalr-user-agent, content-type, authorization, accept, origin, user-agent, range";
        }

        return Task.CompletedTask;
    });

    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = 200;
        await context.Response.WriteAsync("OK");
        return;
    }

    await next();
});

app.UseCors("AllowFrontend");

// Serve frontend SPA static assets (Single Container deployment)
app.UseDefaultFiles();
app.UseStaticFiles();

// 6. Configure Pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<MyTube.Backend.Api.Hubs.LiveSubtitlesHub>("/hubs/live-subtitles");
app.MapHub<MyTube.Backend.Api.Hubs.DownloadHub>("/hubs/downloads");

// Standard ASP.NET Core Database Health Check
app.MapHealthChecks("/health");

// Media tools & storage health check
app.MapGet("/health/media-tools", async (MyTube.Backend.Infrastructure.Media.MediaHealthCheck healthCheck, IConfiguration config) =>
{
    var stagingPath = Path.Combine(Path.GetTempPath(), "mytube_staging");
    var mediaRoot = config["Media:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "media");
    var res = await healthCheck.CheckHealthAsync(stagingPath, mediaRoot);
    return res.IsHealthy ? Results.Ok(res) : Results.Json(res, statusCode: 503);
});

// Fallback for SPA routing in single-container mode
app.MapFallbackToFile("index.html");

// Run SQLite initialization and migrations on startup
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<VideoDbContext>();

        // 1. Configure SQLite WAL mode and concurrency PRAGMAs
        db.Database.OpenConnection();
        using (var cmd = db.Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = @"
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA busy_timeout = 5000;
                PRAGMA foreign_keys = ON;
            ";
            cmd.ExecuteNonQuery();
        }

        // 2. Apply EF Core migrations automatically
        db.Database.Migrate();
        logger.LogInformation("SQLite database migrations applied successfully with WAL mode active.");

        // 3. Safely disable obsolete AI models
        var obsoleteModels = new[] { "llama-3.1-8b-instant", "groq/compound-mini", "gemini-2.5-flash-lite", "gemini-3.1-flash-lite", "gemini-3.5-flash-lite", "gemini-2.5-flash", "gemini-3.5-flash", "gemini-3.6-flash" };
        var obsoleteQuotas = db.AiModelQuotas.Where(q => obsoleteModels.Contains(q.ModelName) && q.IsEnabled).ToList();
        if (obsoleteQuotas.Count > 0)
        {
            foreach (var q in obsoleteQuotas)
            {
                q.Status = "ModelNotFound";
                q.IsEnabled = false;
            }
            db.SaveChanges();
        }

        var now = DateTime.UtcNow;

        // 4. Ensure active models exist in DB
        if (!db.AiModelQuotas.Any(q => q.Provider == "Groq" && q.ModelName == "llama-3.3-70b-versatile"))
        {
            db.AiModelQuotas.Add(new MyTube.Backend.Domain.AiModelQuota { Provider = "Groq", ModelName = "llama-3.3-70b-versatile", RpmLimit = 30, RpdLimit = 1000, TaskType = "Text", PriorityOrder = 1, Status = "Active", IsEnabled = true, WindowStartMinute = now, WindowStartDay = now.Date });
        }
        if (!db.AiModelQuotas.Any(q => q.Provider == "Groq" && q.ModelName == "llama3-8b-8192"))
        {
            db.AiModelQuotas.Add(new MyTube.Backend.Domain.AiModelQuota { Provider = "Groq", ModelName = "llama3-8b-8192", RpmLimit = 30, RpdLimit = 14400, TaskType = "Text", PriorityOrder = 2, Status = "Active", IsEnabled = true, WindowStartMinute = now, WindowStartDay = now.Date });
        }
        if (!db.AiModelQuotas.Any(q => q.Provider == "Gemini" && q.ModelName == "gemini-2.0-flash"))
        {
            db.AiModelQuotas.Add(new MyTube.Backend.Domain.AiModelQuota { Provider = "Gemini", ModelName = "gemini-2.0-flash", RpmLimit = 15, RpdLimit = 1500, TaskType = "Text", PriorityOrder = 3, Status = "Active", IsEnabled = true, WindowStartMinute = now, WindowStartDay = now.Date });
        }
        if (!db.AiModelQuotas.Any(q => q.Provider == "Gemini" && q.ModelName == "gemini-1.5-flash"))
        {
            db.AiModelQuotas.Add(new MyTube.Backend.Domain.AiModelQuota { Provider = "Gemini", ModelName = "gemini-1.5-flash", RpmLimit = 15, RpdLimit = 1500, TaskType = "Text", PriorityOrder = 4, Status = "Active", IsEnabled = true, WindowStartMinute = now, WindowStartDay = now.Date });
        }
        if (!db.AiModelQuotas.Any(q => q.Provider == "Gemini" && q.ModelName == "gemini-1.5-pro"))
        {
            db.AiModelQuotas.Add(new MyTube.Backend.Domain.AiModelQuota { Provider = "Gemini", ModelName = "gemini-1.5-pro", RpmLimit = 15, RpdLimit = 1500, TaskType = "Text", PriorityOrder = 5, Status = "Active", IsEnabled = true, WindowStartMinute = now, WindowStartDay = now.Date });
        }
        if (!db.AiModelQuotas.Any(q => q.Provider == "Gemini" && q.ModelName == "gemini-1.5-flash-8b"))
        {
            db.AiModelQuotas.Add(new MyTube.Backend.Domain.AiModelQuota { Provider = "Gemini", ModelName = "gemini-1.5-flash-8b", RpmLimit = 15, RpdLimit = 1500, TaskType = "Text", PriorityOrder = 6, Status = "Active", IsEnabled = true, WindowStartMinute = now, WindowStartDay = now.Date });
        }
        if (!db.AiModelQuotas.Any(q => q.Provider == "Groq" && q.ModelName == "whisper-large-v3-turbo"))
        {
            db.AiModelQuotas.Add(new MyTube.Backend.Domain.AiModelQuota { Provider = "Groq", ModelName = "whisper-large-v3-turbo", RpmLimit = 20, RpdLimit = 2000, AudioSecondsPerDayLimit = 28800, TaskType = "STT", PriorityOrder = 1, Status = "Active", IsEnabled = true, WindowStartMinute = now, WindowStartDay = now.Date });
        }
        if (!db.AiModelQuotas.Any(q => q.Provider == "DeepSeek" && q.ModelName == "deepseek-chat"))
        {
            db.AiModelQuotas.Add(new MyTube.Backend.Domain.AiModelQuota { Provider = "DeepSeek", ModelName = "deepseek-chat", RpmLimit = 10000, RpdLimit = 100000, TaskType = "Text", PriorityOrder = 7, Status = "Active", IsEnabled = true, WindowStartMinute = now, WindowStartDay = now.Date });
        }

        // 5. Ensure default system category "Downloads" exists
        var defaultCat = db.Categories.FirstOrDefault(c => c.Name == "Downloads");
        if (defaultCat == null)
        {
            defaultCat = new MyTube.Backend.Domain.Category { Name = "Downloads", IsUserCreated = false };
            db.Categories.Add(defaultCat);
        }

        db.SaveChanges();
        logger.LogInformation("Verified SQLite database schema, AI Quotas, and default categories.");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "FATAL: Database initialization or migration failed. Halting application startup.");
        throw;
    }
}

app.Run();
