using System;
using System.IO;
using System.Text;
using MyTube.Backend.Infrastructure.Media;
using Xunit;

namespace MyTube.Backend.Tests;

public class SafePathHelperTests
{
    private const string UserReportedFailingTitle = 
        "ಇರಾನ್ನಲ್ಲಿ ಲೇಡಿ ಸ್ಪೈ.!ಯಹೂದಿ..ಕ್ರೈಸ್ತ..ಸುನ್ನಿ..ಶಿಯಾ..ಜರ್ನಲಿಸ್ಟ್..ಮೊಸಾದ್.？ಸ್ಪೈ ಜಗತ್ತಿನ ರೋಚಕ ಕಹಾನಿ..!";

    [Fact]
    public void RemoveEmojis_StripsAllEmojisWhilePreservingNaturalLanguages()
    {
        var inputWithEmojis = "🔥 Incredible Video! 🚀 (4K 60fps) 🎬🍿";
        var cleaned = SafePathHelper.RemoveEmojis(inputWithEmojis);
        Assert.Equal(" Incredible Video!  (4K 60fps) ", cleaned);

        // Multi-byte script like Kannada must be completely preserved
        var kannadaWithEmojis = "🔥 " + UserReportedFailingTitle + " 🚀";
        var cleanedKannada = SafePathHelper.RemoveEmojis(kannadaWithEmojis);
        Assert.DoesNotContain("🔥", cleanedKannada);
        Assert.DoesNotContain("🚀", cleanedKannada);
        Assert.Contains("ಇರಾನ್ನಲ್ಲಿ", cleanedKannada);
    }

    [Fact]
    public void RemoveUnwantedCharacters_CleansPunctuationClutterAndFullwidthChars()
    {
        var cluttered = "Movie..! Title.... Part 1!!!??? With fullwidth？and：colon";
        var cleaned = SafePathHelper.RemoveUnwantedCharacters(cluttered);

        Assert.DoesNotContain("..", cleaned);
        Assert.DoesNotContain("？", cleaned);
        Assert.DoesNotContain("：", cleaned);
        Assert.DoesNotContain("?", cleaned);
        Assert.Contains("Movie! Title Part 1!", cleaned);
    }

    [Fact]
    public void SanitizeFileName_StripsEmojisAndUnwantedCharsAndTrimsLength()
    {
        var raw = "🔥 BEST MOVIE EVER?!... 🎬 (2026) [1080p] 🚀";
        var sanitized = SafePathHelper.SanitizeFileName(raw);

        Assert.DoesNotContain("🔥", sanitized);
        Assert.DoesNotContain("🎬", sanitized);
        Assert.DoesNotContain("🚀", sanitized);
        Assert.DoesNotContain("?", sanitized);
        Assert.DoesNotContain("...", sanitized);
        Assert.Equal("BEST MOVIE EVER! (2026) [1080p]", sanitized);
    }

    [Fact]
    public void TruncateTitle_UserReportedKannadaTitle_IsCleanedAndTrimmed()
    {
        var sanitized = SafePathHelper.SanitizeFileName(UserReportedFailingTitle);

        Assert.DoesNotContain("..", sanitized);
        Assert.DoesNotContain("？", sanitized);
        Assert.True(Encoding.UTF8.GetByteCount(sanitized) <= 180, "Must be <= 180 bytes");
        Assert.True(sanitized.Length <= 80, "Must be <= 80 characters");
        Assert.StartsWith("ಇರಾನ್ನಲ್ಲಿ", sanitized);
    }

    [Fact]
    public void SanitizeDestinationPath_WhenPathIsVeryLong_TrimsTitleToFitPathBudget()
    {
        var deepFolder = Path.Combine("/media/Downloads", new string('a', 180)); // 180+ chars folder
        var longTitle = "A Very Long Video Title That Needs To Be Trimmed When The Folder Path Is Already Deep";

        var targetPath = SafePathHelper.SanitizeDestinationPath(deepFolder, longTitle, ".mp4", maxTotalPathLength: 240);

        Assert.True(targetPath.Length <= 240, $"Total path length {targetPath.Length} exceeded limit 240.");
        Assert.EndsWith(".mp4", targetPath);
        Assert.StartsWith(deepFolder.Replace('\\', '/'), targetPath.Replace('\\', '/'));
    }

    [Fact]
    public void EnsureUniqueFilePath_GuaranteesComponentNeverExceedsByteLimit()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"mytube_unit_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var baseName = SafePathHelper.SanitizeFileName(UserReportedFailingTitle, 180);
            var initialFile = Path.Combine(tempDir, $"{baseName}.mp4");
            File.WriteAllText(initialFile, "test");

            // First duplicate
            var duplicatePath1 = SafePathHelper.EnsureUniqueFilePath(initialFile, 180);
            File.WriteAllText(duplicatePath1, "test2");

            Assert.NotEqual(initialFile, duplicatePath1);
            Assert.True(Encoding.UTF8.GetByteCount(Path.GetFileName(duplicatePath1)) <= 190);
            Assert.True(Encoding.UTF8.GetByteCount(Path.GetFileName(duplicatePath1)) <= 255);

            // Second duplicate
            var duplicatePath2 = SafePathHelper.EnsureUniqueFilePath(initialFile, 180);
            Assert.NotEqual(duplicatePath1, duplicatePath2);
            Assert.True(Encoding.UTF8.GetByteCount(Path.GetFileName(duplicatePath2)) <= 190);
            Assert.True(Encoding.UTF8.GetByteCount(Path.GetFileName(duplicatePath2)) <= 255);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
