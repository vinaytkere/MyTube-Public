using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MyTube.Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate_Sqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiModelQuotas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    ModelName = table.Column<string>(type: "TEXT", nullable: false),
                    RpmLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    RpdLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    AudioSecondsPerDayLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentMinuteRequests = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentDayRequests = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentDayAudioSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    WindowStartMinute = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WindowStartDay = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    PriorityOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    TaskType = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    LastCheckedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastErrorDetails = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiModelQuotas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsUserCreated = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DownloadBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CategoryName = table.Column<string>(type: "TEXT", nullable: true),
                    TotalItems = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedItems = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedItems = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IptvCategories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IptvCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IptvCountries",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Flag = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    LanguagesJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IptvCountries", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "IptvLanguages",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IptvLanguages", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "IptvRegions",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IptvRegions", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "IptvSyncStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DatasetName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ETag = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSyncAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ItemCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IptvSyncStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OtpRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsUsed = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FullName = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    IsEmailVerified = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Videos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    DisplayTitle = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Duration = table.Column<double>(type: "REAL", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoCodec = table.Column<string>(type: "TEXT", nullable: false),
                    AudioCodec = table.Column<string>(type: "TEXT", nullable: false),
                    ContainerFormat = table.Column<string>(type: "TEXT", nullable: false),
                    FrameRate = table.Column<double>(type: "REAL", nullable: false),
                    ThumbnailPath = table.Column<string>(type: "TEXT", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    ScanStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessingError = table.Column<string>(type: "TEXT", nullable: true),
                    AudioTracksJson = table.Column<string>(type: "TEXT", nullable: true),
                    Transcript = table.Column<string>(type: "TEXT", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: true),
                    DetectedLanguage = table.Column<string>(type: "TEXT", nullable: true),
                    AiStatus = table.Column<string>(type: "TEXT", nullable: false),
                    AiProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RawDescription = table.Column<string>(type: "TEXT", nullable: true),
                    Uploader = table.Column<string>(type: "TEXT", nullable: true),
                    MediaTypeDetected = table.Column<string>(type: "TEXT", nullable: true),
                    PrimaryGenre = table.Column<string>(type: "TEXT", nullable: true),
                    ReleaseYear = table.Column<int>(type: "INTEGER", nullable: true),
                    IsOrganized = table.Column<bool>(type: "INTEGER", nullable: false),
                    OrganizedPath = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Videos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Videos_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DownloadJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Portal = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    CategoryName = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Phase = table.Column<string>(type: "TEXT", nullable: false),
                    Progress = table.Column<double>(type: "REAL", nullable: false),
                    Speed = table.Column<string>(type: "TEXT", nullable: false),
                    Eta = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", nullable: true),
                    MediaType = table.Column<string>(type: "TEXT", nullable: false),
                    Format = table.Column<string>(type: "TEXT", nullable: false),
                    Quality = table.Column<string>(type: "TEXT", nullable: true),
                    IncludeSubtitles = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubtitleLanguage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadJobs_DownloadBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "DownloadBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IptvChannels",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    AltNames = table.Column<string>(type: "TEXT", nullable: true),
                    Network = table.Column<string>(type: "TEXT", nullable: true),
                    CountryCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Subdivision = table.Column<string>(type: "TEXT", nullable: true),
                    City = table.Column<string>(type: "TEXT", nullable: true),
                    Website = table.Column<string>(type: "TEXT", nullable: true),
                    LogoUrl = table.Column<string>(type: "TEXT", nullable: true),
                    PrimaryLanguageCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    IsNsfw = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsClosed = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IptvChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IptvChannels_IptvCountries_CountryCode",
                        column: x => x.CountryCode,
                        principalTable: "IptvCountries",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_IptvChannels_IptvLanguages_PrimaryLanguageCode",
                        column: x => x.PrimaryLanguageCode,
                        principalTable: "IptvLanguages",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "IptvRegionCountries",
                columns: table => new
                {
                    RegionCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CountryCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IptvRegionCountries", x => new { x.RegionCode, x.CountryCode });
                    table.ForeignKey(
                        name: "FK_IptvRegionCountries_IptvCountries_CountryCode",
                        column: x => x.CountryCode,
                        principalTable: "IptvCountries",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IptvRegionCountries_IptvRegions_RegionCode",
                        column: x => x.RegionCode,
                        principalTable: "IptvRegions",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Favorites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    VideoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Favorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Favorites_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoQas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VideoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Question = table.Column<string>(type: "TEXT", nullable: false),
                    Answer = table.Column<string>(type: "TEXT", nullable: false),
                    FirecrawlContext = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoQas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoQas_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WatchProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    VideoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PositionSeconds = table.Column<double>(type: "REAL", nullable: false),
                    DurationSeconds = table.Column<double>(type: "REAL", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Completed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchProgresses_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IptvChannelCategories",
                columns: table => new
                {
                    ChannelId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CategoryId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IptvChannelCategories", x => new { x.ChannelId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_IptvChannelCategories_IptvCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "IptvCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IptvChannelCategories_IptvChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "IptvChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IptvFavorites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ChannelId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IptvFavorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IptvFavorites_IptvChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "IptvChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IptvStreams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChannelId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Feed = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Quality = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", nullable: true),
                    Referrer = table.Column<string>(type: "TEXT", nullable: true),
                    Labels = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LastCheckedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OrderIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IptvStreams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IptvStreams_IptvChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "IptvChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IptvWatchHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ChannelId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    LastStreamId = table.Column<int>(type: "INTEGER", nullable: true),
                    WatchedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IptvWatchHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IptvWatchHistories_IptvChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "IptvChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IptvWatchHistories_IptvStreams_LastStreamId",
                        column: x => x.LastStreamId,
                        principalTable: "IptvStreams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "AiModelQuotas",
                columns: new[] { "Id", "AudioSecondsPerDayLimit", "CurrentDayAudioSeconds", "CurrentDayRequests", "CurrentMinuteRequests", "IsEnabled", "LastCheckedAt", "LastErrorDetails", "ModelName", "PriorityOrder", "Provider", "RpdLimit", "RpmLimit", "Status", "TaskType", "WindowStartDay", "WindowStartMinute" },
                values: new object[,]
                {
                    { 1, 28800, 0, 0, 0, true, null, null, "whisper-large-v3-turbo", 1, "Groq", 2000, 20, "Active", "STT", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, 0, 0, 0, 0, false, null, null, "llama-3.1-8b-instant", 99, "Groq", 14400, 30, "ModelNotFound", "Text", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, 0, 0, 0, 0, false, null, null, "gemini-2.5-flash-lite", 99, "Gemini", 1000, 15, "ModelNotFound", "Text", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, 0, 0, 0, 0, false, null, null, "gemini-3.1-flash-lite", 99, "Gemini", 1000, 15, "ModelNotFound", "Text", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, 0, 0, 0, 0, false, null, null, "gemini-3.5-flash-lite", 99, "Gemini", 1000, 15, "ModelNotFound", "Text", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, 0, 0, 0, 0, true, null, null, "gemini-2.0-flash", 2, "Gemini", 1500, 15, "Active", "Text", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 7, 0, 0, 0, 0, true, null, null, "gemini-1.5-flash", 3, "Gemini", 1500, 15, "Active", "Text", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 8, 0, 0, 0, 0, true, null, null, "llama-3.3-70b-versatile", 1, "Groq", 1000, 30, "Active", "Text", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 9, 0, 0, 0, 0, true, null, null, "deepseek-chat", 4, "DeepSeek", 100000, 10000, "Active", "Text", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 10, 0, 0, 0, 0, true, null, null, "llama3-8b-8192", 5, "Groq", 14400, 30, "Active", "Text", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 11, 0, 0, 0, 0, true, null, null, "gemini-1.5-pro", 6, "Gemini", 1500, 15, "Active", "Text", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 12, 0, 0, 0, 0, true, null, null, "gemini-1.5-flash-8b", 7, "Gemini", 1500, 15, "Active", "Text", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiModelQuotas_Provider_ModelName",
                table: "AiModelQuotas",
                columns: new[] { "Provider", "ModelName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DownloadJobs_BatchId",
                table: "DownloadJobs",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadJobs_Status",
                table: "DownloadJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_UserId_VideoId",
                table: "Favorites",
                columns: new[] { "UserId", "VideoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_VideoId",
                table: "Favorites",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "IX_IptvChannelCategories_CategoryId",
                table: "IptvChannelCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_IptvChannels_CountryCode",
                table: "IptvChannels",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_IptvChannels_IsActive",
                table: "IptvChannels",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_IptvChannels_Name",
                table: "IptvChannels",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_IptvChannels_PrimaryLanguageCode",
                table: "IptvChannels",
                column: "PrimaryLanguageCode");

            migrationBuilder.CreateIndex(
                name: "IX_IptvFavorites_ChannelId",
                table: "IptvFavorites",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_IptvFavorites_UserId_ChannelId",
                table: "IptvFavorites",
                columns: new[] { "UserId", "ChannelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IptvRegionCountries_CountryCode",
                table: "IptvRegionCountries",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_IptvStreams_ChannelId",
                table: "IptvStreams",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_IptvStreams_IsActive",
                table: "IptvStreams",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_IptvSyncStates_DatasetName",
                table: "IptvSyncStates",
                column: "DatasetName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IptvWatchHistories_ChannelId",
                table: "IptvWatchHistories",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_IptvWatchHistories_LastStreamId",
                table: "IptvWatchHistories",
                column: "LastStreamId");

            migrationBuilder.CreateIndex(
                name: "IX_IptvWatchHistories_UserId_WatchedAt",
                table: "IptvWatchHistories",
                columns: new[] { "UserId", "WatchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OtpRecords_Email",
                table: "OtpRecords",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoQas_VideoId",
                table: "VideoQas",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_CategoryId",
                table: "Videos",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_FilePath",
                table: "Videos",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchProgresses_UserId_VideoId",
                table: "WatchProgresses",
                columns: new[] { "UserId", "VideoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchProgresses_VideoId",
                table: "WatchProgresses",
                column: "VideoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiModelQuotas");

            migrationBuilder.DropTable(
                name: "DownloadJobs");

            migrationBuilder.DropTable(
                name: "Favorites");

            migrationBuilder.DropTable(
                name: "IptvChannelCategories");

            migrationBuilder.DropTable(
                name: "IptvFavorites");

            migrationBuilder.DropTable(
                name: "IptvRegionCountries");

            migrationBuilder.DropTable(
                name: "IptvSyncStates");

            migrationBuilder.DropTable(
                name: "IptvWatchHistories");

            migrationBuilder.DropTable(
                name: "OtpRecords");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "VideoQas");

            migrationBuilder.DropTable(
                name: "WatchProgresses");

            migrationBuilder.DropTable(
                name: "DownloadBatches");

            migrationBuilder.DropTable(
                name: "IptvCategories");

            migrationBuilder.DropTable(
                name: "IptvRegions");

            migrationBuilder.DropTable(
                name: "IptvStreams");

            migrationBuilder.DropTable(
                name: "Videos");

            migrationBuilder.DropTable(
                name: "IptvChannels");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "IptvCountries");

            migrationBuilder.DropTable(
                name: "IptvLanguages");
        }
    }
}
