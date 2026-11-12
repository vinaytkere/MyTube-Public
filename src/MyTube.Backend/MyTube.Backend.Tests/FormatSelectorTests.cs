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
}
