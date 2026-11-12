using Microsoft.EntityFrameworkCore;
using MyTube.Backend.Domain;

namespace MyTube.Backend.Infrastructure;

public class VideoDbContext : DbContext
{
    public VideoDbContext(DbContextOptions<VideoDbContext> options) : base(options)
    {
    }

    public DbSet<Video> Videos => Set<Video>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<WatchProgress> WatchProgresses => Set<WatchProgress>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<AiModelQuota> AiModelQuotas => Set<AiModelQuota>();
    public DbSet<User> Users => Set<User>();
    public DbSet<OtpRecord> OtpRecords => Set<OtpRecord>();
    public DbSet<VideoQa> VideoQas => Set<VideoQa>();
    public DbSet<DownloadBatch> DownloadBatches => Set<DownloadBatch>();
    public DbSet<DownloadJobRecord> DownloadJobs => Set<DownloadJobRecord>();

    // IPTV DbSets
    public DbSet<IptvChannel> IptvChannels => Set<IptvChannel>();
    public DbSet<IptvStream> IptvStreams => Set<IptvStream>();
    public DbSet<IptvCountry> IptvCountries => Set<IptvCountry>();
    public DbSet<IptvRegion> IptvRegions => Set<IptvRegion>();
    public DbSet<IptvRegionCountry> IptvRegionCountries => Set<IptvRegionCountry>();
    public DbSet<IptvCategory> IptvCategories => Set<IptvCategory>();
    public DbSet<IptvChannelCategory> IptvChannelCategories => Set<IptvChannelCategory>();
    public DbSet<IptvLanguage> IptvLanguages => Set<IptvLanguage>();
    public DbSet<IptvFavorite> IptvFavorites => Set<IptvFavorite>();
    public DbSet<IptvWatchHistory> IptvWatchHistories => Set<IptvWatchHistory>();
    public DbSet<IptvSyncState> IptvSyncStates => Set<IptvSyncState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DownloadBatch>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.HasMany(b => b.Jobs)
                .WithOne(j => j.Batch)
                .HasForeignKey(j => j.BatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DownloadJobRecord>(entity =>
        {
            entity.HasKey(j => j.Id);
            entity.HasIndex(j => j.BatchId);
            entity.HasIndex(j => j.Status);
            entity.Property(j => j.Url).IsRequired();
        });

        modelBuilder.Entity<VideoQa>(entity =>
        {
            entity.HasKey(q => q.Id);
            entity.Property(q => q.Question).IsRequired();
            entity.Property(q => q.Answer).IsRequired();
            entity.HasIndex(q => q.VideoId);

            entity.HasOne(q => q.Video)
                .WithMany()
                .HasForeignKey(q => q.VideoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Username).IsRequired().HasMaxLength(100);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(255);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<OtpRecord>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Email).IsRequired().HasMaxLength(255);
            entity.Property(o => o.Code).IsRequired().HasMaxLength(10);
            entity.HasIndex(o => o.Email);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<Video>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.FilePath).IsRequired().HasMaxLength(1000);
            entity.Property(v => v.FileName).IsRequired().HasMaxLength(255);
            entity.Property(v => v.DisplayTitle).IsRequired().HasMaxLength(255);
            entity.Property(v => v.SourceUrl).HasMaxLength(2048);
            
            entity.HasIndex(v => v.FilePath).IsUnique();

            entity.HasOne(v => v.Category)
                .WithMany(c => c.Videos)
                .HasForeignKey(v => v.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WatchProgress>(entity =>
        {
            entity.HasKey(wp => wp.Id);
            entity.Property(wp => wp.UserId).IsRequired().HasMaxLength(100);
            entity.HasIndex(wp => new { wp.UserId, wp.VideoId }).IsUnique();

            entity.HasOne(wp => wp.Video)
                .WithMany()
                .HasForeignKey(wp => wp.VideoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.Property(f => f.UserId).IsRequired().HasMaxLength(100);
            entity.HasIndex(f => new { f.UserId, f.VideoId }).IsUnique();

            entity.HasOne(f => f.Video)
                .WithMany()
                .HasForeignKey(f => f.VideoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AiModelQuota>(entity =>
        {
            entity.HasKey(q => q.Id);
            entity.HasIndex(q => new { q.Provider, q.ModelName }).IsUnique();

            entity.HasData(
                new AiModelQuota { Id = 1, Provider = "Groq", ModelName = "whisper-large-v3-turbo", RpmLimit = 20, RpdLimit = 2000, AudioSecondsPerDayLimit = 28800, TaskType = "STT", PriorityOrder = 1, Status = "Active", IsEnabled = true },
                new AiModelQuota { Id = 2, Provider = "Groq", ModelName = "llama-3.1-8b-instant", RpmLimit = 30, RpdLimit = 14400, AudioSecondsPerDayLimit = 0, TaskType = "Text", PriorityOrder = 99, Status = "ModelNotFound", IsEnabled = false },
                new AiModelQuota { Id = 3, Provider = "Gemini", ModelName = "gemini-2.5-flash-lite", RpmLimit = 15, RpdLimit = 1000, AudioSecondsPerDayLimit = 0, TaskType = "Text", PriorityOrder = 99, Status = "ModelNotFound", IsEnabled = false },
                new AiModelQuota { Id = 4, Provider = "Gemini", ModelName = "gemini-3.1-flash-lite", RpmLimit = 15, RpdLimit = 1000, AudioSecondsPerDayLimit = 0, TaskType = "Text", PriorityOrder = 99, Status = "ModelNotFound", IsEnabled = false },
                new AiModelQuota { Id = 5, Provider = "Gemini", ModelName = "gemini-3.5-flash-lite", RpmLimit = 15, RpdLimit = 1000, AudioSecondsPerDayLimit = 0, TaskType = "Text", PriorityOrder = 99, Status = "ModelNotFound", IsEnabled = false },
                new AiModelQuota { Id = 6, Provider = "Gemini", ModelName = "gemini-2.0-flash", RpmLimit = 15, RpdLimit = 1500, AudioSecondsPerDayLimit = 0, TaskType = "Text", PriorityOrder = 2, Status = "Active", IsEnabled = true },
                new AiModelQuota { Id = 7, Provider = "Gemini", ModelName = "gemini-1.5-flash", RpmLimit = 15, RpdLimit = 1500, AudioSecondsPerDayLimit = 0, TaskType = "Text", PriorityOrder = 3, Status = "Active", IsEnabled = true },
                new AiModelQuota { Id = 8, Provider = "Groq", ModelName = "llama-3.3-70b-versatile", RpmLimit = 30, RpdLimit = 1000, AudioSecondsPerDayLimit = 0, TaskType = "Text", PriorityOrder = 1, Status = "Active", IsEnabled = true },
                new AiModelQuota { Id = 9, Provider = "DeepSeek", ModelName = "deepseek-chat", RpmLimit = 10000, RpdLimit = 100000, AudioSecondsPerDayLimit = 0, TaskType = "Text", PriorityOrder = 4, Status = "Active", IsEnabled = true },
                new AiModelQuota { Id = 10, Provider = "Groq", ModelName = "llama3-8b-8192", RpmLimit = 30, RpdLimit = 14400, AudioSecondsPerDayLimit = 0, TaskType = "Text", PriorityOrder = 5, Status = "Active", IsEnabled = true },
                new AiModelQuota { Id = 11, Provider = "Gemini", ModelName = "gemini-1.5-pro", RpmLimit = 15, RpdLimit = 1500, AudioSecondsPerDayLimit = 0, TaskType = "Text", PriorityOrder = 6, Status = "Active", IsEnabled = true },
                new AiModelQuota { Id = 12, Provider = "Gemini", ModelName = "gemini-1.5-flash-8b", RpmLimit = 15, RpdLimit = 1500, AudioSecondsPerDayLimit = 0, TaskType = "Text", PriorityOrder = 7, Status = "Active", IsEnabled = true }
            );
        });

        // IPTV Entity Configurations
        modelBuilder.Entity<IptvChannel>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).HasMaxLength(128);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(255);
            entity.Property(c => c.CountryCode).HasMaxLength(10);
            entity.Property(c => c.PrimaryLanguageCode).HasMaxLength(10);
            entity.Property(c => c.Website);
            entity.Property(c => c.LogoUrl);

            entity.HasIndex(c => c.CountryCode);
            entity.HasIndex(c => c.PrimaryLanguageCode);
            entity.HasIndex(c => c.IsActive);
            entity.HasIndex(c => c.Name);

            entity.HasOne(c => c.Country)
                .WithMany(co => co.Channels)
                .HasForeignKey(c => c.CountryCode)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(c => c.PrimaryLanguage)
                .WithMany(l => l.Channels)
                .HasForeignKey(c => c.PrimaryLanguageCode)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(c => c.Streams)
                .WithOne(s => s.Channel)
                .HasForeignKey(s => s.ChannelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IptvStream>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.ChannelId).IsRequired().HasMaxLength(128);
            entity.Property(s => s.Url).IsRequired();
            entity.Property(s => s.Quality).HasMaxLength(50);
            entity.Property(s => s.Feed).HasMaxLength(100);
            entity.Property(s => s.Status).IsRequired().HasMaxLength(50);

            entity.HasIndex(s => s.ChannelId);
            entity.HasIndex(s => s.IsActive);
        });

        modelBuilder.Entity<IptvCountry>(entity =>
        {
            entity.HasKey(c => c.Code);
            entity.Property(c => c.Code).HasMaxLength(10);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(200);
            entity.Property(c => c.Flag).HasMaxLength(20);
        });

        modelBuilder.Entity<IptvRegion>(entity =>
        {
            entity.HasKey(r => r.Code);
            entity.Property(r => r.Code).HasMaxLength(20);
            entity.Property(r => r.Name).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<IptvRegionCountry>(entity =>
        {
            entity.HasKey(rc => new { rc.RegionCode, rc.CountryCode });
            entity.Property(rc => rc.RegionCode).HasMaxLength(20);
            entity.Property(rc => rc.CountryCode).HasMaxLength(10);

            entity.HasOne(rc => rc.Region)
                .WithMany(r => r.RegionCountries)
                .HasForeignKey(rc => rc.RegionCode)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(rc => rc.Country)
                .WithMany(c => c.RegionCountries)
                .HasForeignKey(rc => rc.CountryCode)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IptvCategory>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).HasMaxLength(100);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(150);
        });

        modelBuilder.Entity<IptvChannelCategory>(entity =>
        {
            entity.HasKey(cc => new { cc.ChannelId, cc.CategoryId });
            entity.Property(cc => cc.ChannelId).HasMaxLength(128);
            entity.Property(cc => cc.CategoryId).HasMaxLength(100);

            entity.HasOne(cc => cc.Channel)
                .WithMany(c => c.ChannelCategories)
                .HasForeignKey(cc => cc.ChannelId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(cc => cc.Category)
                .WithMany(cat => cat.ChannelCategories)
                .HasForeignKey(cc => cc.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IptvLanguage>(entity =>
        {
            entity.HasKey(l => l.Code);
            entity.Property(l => l.Code).HasMaxLength(10);
            entity.Property(l => l.Name).IsRequired().HasMaxLength(150);
        });

        modelBuilder.Entity<IptvFavorite>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.Property(f => f.UserId).IsRequired().HasMaxLength(100);
            entity.Property(f => f.ChannelId).IsRequired().HasMaxLength(128);
            entity.HasIndex(f => new { f.UserId, f.ChannelId }).IsUnique();

            entity.HasOne(f => f.Channel)
                .WithMany()
                .HasForeignKey(f => f.ChannelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IptvWatchHistory>(entity =>
        {
            entity.HasKey(h => h.Id);
            entity.Property(h => h.UserId).IsRequired().HasMaxLength(100);
            entity.Property(h => h.ChannelId).IsRequired().HasMaxLength(128);
            entity.HasIndex(h => new { h.UserId, h.WatchedAt });

            entity.HasOne(h => h.Channel)
                .WithMany()
                .HasForeignKey(h => h.ChannelId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(h => h.LastStream)
                .WithMany()
                .HasForeignKey(h => h.LastStreamId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<IptvSyncState>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.DatasetName).IsRequired().HasMaxLength(100);
            entity.HasIndex(s => s.DatasetName).IsUnique();
        });
    }
}
