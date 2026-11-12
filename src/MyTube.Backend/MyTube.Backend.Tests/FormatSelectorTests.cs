using MyTube.Backend.Application;
using MyTube.Backend.Infrastructure;
using Xunit;

namespace MyTube.Backend.Tests;

public class FormatSelectorTests
{
    [Theory]
    [InlineData("1080p", "bestvideo[height<=1080][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<=1080]+bestaudio/best[height<=1080]/best")]
    [InlineData("1080p (Full HD)", "bestvideo[height<=1080][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<=1080]+bestaudio/best[height<=1080]/best")]
    [InlineData("720p (HD)", "bestvideo[height<=720][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<=720]+bestaudio/best[height<=720]/best")]
    [InlineData("480p", "bestvideo[height<=480][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<=480]+bestaudio/best[height<=480]/best")]
    [InlineData("360p (Low)", "bestvideo[height<=360][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<=360]+bestaudio/best[height<=360]/best")]
    public void BuildFormatSelector_WhenMp4AndHeightUnder1080_PrioritizesNativeMp4Streams(string quality, string expected)
    {
        var result = DownloadManager.BuildFormatSelector(quality, OutputFormat.Mp4);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildFormatSelector_WhenDefaultOrEmpty_PrioritizesNativeMp4()
    {
        var resultDefault = DownloadManager.BuildFormatSelector("Top Quality Available (Default)", OutputFormat.Mp4);
        var resultNull = DownloadManager.BuildFormatSelector(null, OutputFormat.Mp4);
        var resultEmpty = DownloadManager.BuildFormatSelector("", OutputFormat.Mp4);

        const string expected = "bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo+bestaudio/best";
        Assert.Equal(expected, resultDefault);
        Assert.Equal(expected, resultNull);
        Assert.Equal(expected, resultEmpty);
    }

    [Theory]
    [InlineData("1440p (2K QHD)", "bestvideo[height<=1440]+bestaudio/best[height<=1440]/best")]
    [InlineData("2160p (4K UHD)", "bestvideo[height<=2160]+bestaudio/best[height<=2160]/best")]
    public void BuildFormatSelector_WhenQualityAbove1080p_AllowsHighestQualityWithoutForcingMp4Video(string quality, string expected)
    {
        var result = DownloadManager.BuildFormatSelector(quality, OutputFormat.Mp4);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildFormatSelector_WithSingleAudioLanguage_IncludesPrefixFallback()
    {
        var result = DownloadManager.BuildFormatSelector(null, OutputFormat.Mp4, "en");
        const string expected = "bestvideo[ext=mp4]+bestaudio[language=en]/bestvideo[ext=mp4]+bestaudio[language^=en]/bestvideo[ext=mp4]+bestaudio/best";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildFormatSelector_WithQualityAndAudioLanguage_AppliesBothFilters()
    {
        var result = DownloadManager.BuildFormatSelector("720p", OutputFormat.Mp4, "es");
        const string expected = "bestvideo[height<=720][ext=mp4]+bestaudio[language=es]/bestvideo[height<=720][ext=mp4]+bestaudio[language^=es]/bestvideo[height<=720][ext=mp4]+bestaudio/best";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildFormatSelector_WithAllAudioLanguages_GeneratesMergeallSelector()
    {
        var result = DownloadManager.BuildFormatSelector(null, OutputFormat.Mp4, "all");
        const string expected = "bestvideo+mergeall[vcodec=none]/bestvideo+bestaudio/best";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildFormatSelector_WithMultipleCommaSeparatedAudioLanguages_GeneratesMultiAudioSelector()
    {
        var result = DownloadManager.BuildFormatSelector("1080p", OutputFormat.Mp4, "en,es");
        const string expected = "bestvideo[height<=1080]+bestaudio[language=en]+bestaudio[language=es]/bestvideo[height<=1080]+bestaudio/best";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeSubtitleLanguage_WhenNullOrAuto_ReturnsSafeAutoSubs()
    {
        const string safeAutoSubs = ".*-orig,en-orig,en,en-US,en-GB,es-orig,es,es-419,fr-orig,fr,de-orig,de,hi-orig,hi,ja-orig,ja,zh-Hans,zh-Hant";
        Assert.Equal(safeAutoSubs, DownloadManager.NormalizeSubtitleLanguage(null));
        Assert.Equal(safeAutoSubs, DownloadManager.NormalizeSubtitleLanguage(""));
        Assert.Equal(safeAutoSubs, DownloadManager.NormalizeSubtitleLanguage("auto"));
    }

    [Fact]
    public void NormalizeSubtitleLanguage_WhenAll_ReturnsExclusionsForMachineTranslatedSubs()
    {
        const string safeAllSubs = "all,-live_chat,-.*-.*,.*-orig,en-US,en-GB,es-419,pt-BR,pt-PT,zh-Hans,zh-Hant";
        Assert.Equal(safeAllSubs, DownloadManager.NormalizeSubtitleLanguage("all"));
    }

    [Fact]
    public void NormalizeSubtitleLanguage_WhenEnglish_ReturnsEnglishVariants()
    {
        Assert.Equal("en-orig,en,en-US,en-GB", DownloadManager.NormalizeSubtitleLanguage("en"));
        Assert.Equal("en-orig,en,en-US,en-GB", DownloadManager.NormalizeSubtitleLanguage("en.*"));
        Assert.Equal("en-orig,en,en-US,en-GB", DownloadManager.NormalizeSubtitleLanguage("en.*,en"));
    }

    [Fact]
    public void NormalizeSubtitleLanguage_WhenSpanishOrPortuguese_ReturnsExpandedDialects()
    {
        Assert.Equal("es-orig,es,es-419,es-ES", DownloadManager.NormalizeSubtitleLanguage("es"));
        Assert.Equal("pt-orig,pt,pt-BR,pt-PT", DownloadManager.NormalizeSubtitleLanguage("pt"));
    }

    [Fact]
    public void NormalizeSubtitleLanguage_WithProbeOriginalTrack_UsesProbeTrack()
    {
        var probe = new VideoProbeResultDto
        {
            SubtitleTracks = new()
            {
                new MediaTrackInfoDto { Code = "ja-orig", Name = "Japanese", IsOriginal = true },
                new MediaTrackInfoDto { Code = "en", Name = "English", IsOriginal = false }
            }
        };

        var result = DownloadManager.NormalizeSubtitleLanguage(null, probe);
        Assert.Equal("ja-orig,ja", result);
    }

    [Fact]
    public void NormalizeSubtitleLanguage_WithProbeAndAll_ReturnsDiscoveredProbeCodes()
    {
        var probe = new VideoProbeResultDto
        {
            SubtitleTracks = new()
            {
                new MediaTrackInfoDto { Code = "en", Name = "English" },
                new MediaTrackInfoDto { Code = "fr", Name = "French" }
            }
        };

        var result = DownloadManager.NormalizeSubtitleLanguage("all", probe);
        Assert.Equal("en,fr", result);
    }
}
