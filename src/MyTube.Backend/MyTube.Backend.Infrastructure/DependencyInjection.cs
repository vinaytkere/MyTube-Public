using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyTube.Backend.Application;

namespace MyTube.Backend.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=/app/data/mytube.db;Cache=Shared";

        // Ensure database directory exists if path is specified in connection string
        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(connectionString, @"(?:Data Source|Filename)\s*=\s*([^;]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var dbPath = match.Groups[1].Value.Trim();
                var dir = System.IO.Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrWhiteSpace(dir) && !System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }
            }
        }
        catch { }

        services.AddDbContextPool<VideoDbContext>(options =>
        {
            options.UseSqlite(connectionString, b =>
            {
                b.MigrationsAssembly(typeof(VideoDbContext).Assembly.FullName);
                b.CommandTimeout(30);
            });
        });

        services.AddSingleton<IMediaToolRunner, MediaToolRunner>();
        services.AddSingleton<IDownloaderSettingsStore, Media.DownloaderSettingsStore>();
        services.AddSingleton<Media.MediaOutputValidator>();
        services.AddSingleton<Media.IFfmpegRunner, Media.FfmpegRunner>();
        services.AddSingleton<ISourceMetadataService, Media.SourceMetadataService>();
        services.AddSingleton<Media.FfprobeService>();
        services.AddSingleton<IMediaProbeService>(sp => sp.GetRequiredService<Media.FfprobeService>());
        services.AddSingleton<Application.Security.ISecretManagementService, Security.SecretManagementService>();
        services.AddSingleton<Media.FfmpegMediaProcessor>();
        services.AddSingleton<IMediaProcessor>(sp => sp.GetRequiredService<Media.FfmpegMediaProcessor>());
        services.AddSingleton<Media.MediaHealthCheck>();
        services.AddSingleton<IVideoScanner, VideoScanner>();
        services.AddSingleton<IDownloadQueue, DownloadQueue>();
        services.AddSingleton<IDownloadManager, DownloadManager>();
        services.AddScoped<IVideoService, VideoService>();
        services.AddScoped<IEmailService, BrevoEmailService>();
        services.AddHostedService<ScanBackgroundWorker>();
        services.AddHostedService<DownloadBackgroundWorker>();

        // Repositories & Security
        services.AddScoped(typeof(Application.Repositories.IRepository<>), typeof(Repositories.EfRepository<>));
        services.AddScoped<Application.Repositories.IVideoRepository, Repositories.VideoRepository>();
        services.AddScoped<Application.Repositories.ICategoryRepository, Repositories.CategoryRepository>();
        services.AddScoped<Application.Repositories.IUserRepository, Repositories.UserRepository>();
        services.AddScoped<Application.Security.IJwtTokenGenerator, Security.JwtTokenGenerator>();

        // AI Services
        services.AddHttpClient();
        services.AddScoped<Ai.FirecrawlService>();
        services.AddScoped<Application.AI.IFirecrawlService>(sp => sp.GetRequiredService<Ai.FirecrawlService>());
        services.AddScoped<IAiQuotaManager, Ai.AiQuotaManager>();
        services.AddScoped<IAiModelHealthService, Ai.AiModelHealthService>();
        // Unified AI Service (Cerebras, Groq, DeepSeek, Gemini, Ollama, Whisper STT)
        services.AddScoped<Ai.AiService>();
        services.AddScoped<Application.AI.IAiService>(sp => sp.GetRequiredService<Ai.AiService>());
        services.AddScoped<IAiCompletionService>(sp => sp.GetRequiredService<Ai.AiService>());
        services.AddScoped<IAiAudioTranscriptionService>(sp => sp.GetRequiredService<Ai.AiService>());
        services.AddScoped<IAiOrchestrator, Ai.AiOrchestrator>();

        // STT Providers & Router
        services.AddScoped<Ai.Transcription.GroqWhisperProvider>();
        services.AddScoped<Ai.Transcription.GeminiTranscriptionProvider>();
        services.AddScoped<Application.AI.ITranscriptionProvider>(sp => sp.GetRequiredService<Ai.Transcription.GroqWhisperProvider>());
        services.AddScoped<Application.AI.ITranscriptionProvider>(sp => sp.GetRequiredService<Ai.Transcription.GeminiTranscriptionProvider>());
        services.AddScoped<Application.AI.ITranscriptionRouter, Ai.Transcription.AiTranscriptionRouter>();

        // Translation Providers & Router
        services.AddScoped<Ai.Translation.FastLlamaTranslationProvider>();
        services.AddScoped<Ai.Translation.GeminiTranslationProvider>();
        services.AddScoped<Application.AI.ITranslationProvider>(sp => sp.GetRequiredService<Ai.Translation.FastLlamaTranslationProvider>());
        services.AddScoped<Application.AI.ITranslationProvider>(sp => sp.GetRequiredService<Ai.Translation.GeminiTranslationProvider>());
        services.AddScoped<Application.AI.ITranslationRouter, Ai.Translation.AiTranslationRouter>();

        // Live Subtitle Session Service
        services.AddSingleton<Application.AI.ILiveSubtitleService, Ai.LiveSubtitleService>();

        // Radarr Service
        services.AddSingleton<Application.Radarr.IRadarrService, Radarr.RadarrService>();

        // Sonarr Service
        services.AddSingleton<Application.Sonarr.ISonarrService, Sonarr.SonarrService>();

        // File Naming & Organization Service
        services.AddScoped<Application.FileNaming.IMediaNamingService, FileNaming.MediaNamingService>();

        // Media Library Auto Watcher Service
        services.AddHostedService<MediaLibraryWatcherService>();

        // Prowlarr Indexer Service
        services.AddSingleton<Application.Prowlarr.IProwlarrService, Prowlarr.ProwlarrService>();

        // OCI SmartRoute Client & Media Organizer
        services.AddScoped<FileNaming.MediaMetadataHarvester>();
        services.AddHttpClient<Application.AI.ISmartRouteClient, Ai.SmartRouteClient>();
        services.AddScoped<Application.MediaOrganizer.IMediaOrganizerService, FileNaming.MediaOrganizerService>();

        // IPTV Services
        services.AddSingleton<Application.Iptv.ISsrfValidator, Iptv.SsrfValidator>();
        services.AddHttpClient<Iptv.IIptvCatalogClient, Iptv.IptvCatalogClient>();
        services.AddSingleton<Application.Iptv.IIptvSyncService, Iptv.IptvSyncService>();
        services.AddScoped<Application.Iptv.IIptvService, Iptv.IptvService>();
        services.AddHttpClient("IptvStreamRelay", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10)
        });
        services.AddScoped<Application.Iptv.IIptvStreamRelayService, Iptv.IptvStreamRelayService>();
        services.AddHostedService<Iptv.IptvSyncBackgroundWorker>();

        return services;
    }
}
