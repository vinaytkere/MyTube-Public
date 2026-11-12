using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MyTube.Backend.Infrastructure.Media;

public static class SubtitleCleaner
{
    private static readonly Regex TimestampRegex = new(
        @"^(?:\d{1,2}:)?\d{1,2}:\d{1,2}[,\.]\d{1,3}\s*-->\s*(?:\d{1,2}:)?\d{1,2}:\d{1,2}[,\.]\d{1,3}.*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex TagRegex = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex NumberOnlyLineRegex = new(@"^\d+$", RegexOptions.Compiled | RegexOptions.Multiline);

    public static string? ExtractPlainText(string filePath, int maxCharacters = 12000)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;

        try
        {
            var content = File.ReadAllText(filePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(content)) return null;

            // Remove WEBVTT header
            if (content.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase))
            {
                var idx = content.IndexOf("\n\n", StringComparison.Ordinal);
                if (idx > 0)
                {
                    content = content.Substring(idx + 2);
                }
            }

            // Remove SRT cue numbers and timestamps
            content = NumberOnlyLineRegex.Replace(content, "");
            content = TimestampRegex.Replace(content, "");
            content = TagRegex.Replace(content, "");

            // Split into lines, filter out empty and duplicates
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            string? prevLine = null;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("NOTE") || line.StartsWith("STYLE")) continue;
                if (line.Equals(prevLine, StringComparison.OrdinalIgnoreCase)) continue;

                sb.Append(line).Append(' ');
                prevLine = line;

                if (sb.Length >= maxCharacters) break;
            }

            return sb.ToString().Trim();
        }
        catch
        {
            return null;
        }
    }
}
