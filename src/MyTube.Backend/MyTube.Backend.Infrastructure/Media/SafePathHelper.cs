using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MyTube.Backend.Infrastructure.Media;

/// <summary>
/// Provides path and filename sanitization with strict enforcement of filesystem limits (UTF-8 byte length),
/// removal of unwanted characters and emojis, and smart trimming for long titles/paths.
/// Linux filesystems (ext4, tmpfs, etc.) limit individual filename components to 255 bytes (NAME_MAX).
/// Non-Latin characters (Kannada, Hindi, Chinese, etc.) consume 3 bytes each, and emojis consume 4 bytes each.
/// </summary>
public static class SafePathHelper
{
    public const int DefaultMaxStemBytes = 180;
    public const int DefaultMaxStemChars = 80;
    public const int DefaultMaxDirectoryBytes = 180;
    public const int DefaultMaxPathLength = 240;

    private static readonly HashSet<char> ProblematicFileNameChars = new(Path.GetInvalidFileNameChars())
    {
        '?', '*', '"', '<', '>', '|', ':', '/', '\\'
    };

    /// <summary>
    /// Checks whether a Unicode Rune represents an emoji, emoji symbol, or emoji modifier.
    /// </summary>
    public static bool IsEmoji(Rune rune)
    {
        int val = rune.Value;

        // Plane 1: Emoticons, Miscellaneous Symbols and Pictographs, Transport, Supplemental, etc.
        // U+1F000 - U+1FAFF covers almost all standard emojis, pictographs, and symbols.
        if (val >= 0x1F000 && val <= 0x1FAFF) return true;

        // BMP: Miscellaneous Symbols (U+2600 - U+26FF) and Dingbats (U+2700 - U+27BF)
        if (val >= 0x2600 && val <= 0x27BF) return true;

        // BMP: Miscellaneous Technical (U+2300 - U+23FF) like watch, hourglass, playback controls
        if (val >= 0x2300 && val <= 0x23FF) return true;

        // BMP: Arrows and Supplemental Symbols (U+2B00 - U+2BFF) like stars, squares, arrows
        if (val >= 0x2B00 && val <= 0x2BFF) return true;

        // Variation Selectors (U+FE00 - U+FE0F, especially U+FE0F emoji presentation)
        if (val >= 0xFE00 && val <= 0xFE0F) return true;

        // Zero-width joiner & keycap
        if (val == 0x200D || val == 0x20E3) return true;

        // Enclosed Alphanumeric and other specific symbols used as emojis
        if (val >= 0x2460 && val <= 0x24FF) return true;

        if (val == 0x203C || val == 0x2049 || val == 0x2122 || val == 0x2139 ||
            (val >= 0x2194 && val <= 0x21AA) || val == 0x25AA || val == 0x25AB ||
            val == 0x25B6 || val == 0x25C0 || (val >= 0x25FB && val <= 0x25FE) ||
            val == 0x2934 || val == 0x2935 || val == 0x3030 || val == 0x303D ||
            val == 0x3297 || val == 0x3299)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes all emojis and emoji-presentation characters from a string.
    /// Preserves all natural language alphabets, vowels, and scripts (Latin, Kannada, Hindi, CJK, Arabic, etc.).
    /// </summary>
    public static string RemoveEmojis(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var sb = new StringBuilder(input.Length);
        foreach (var rune in input.EnumerateRunes())
        {
            if (!IsEmoji(rune))
            {
                sb.Append(rune.ToString());
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Removes emojis and unwanted clutter characters (illegal filename chars, fullwidth symbols,
    /// repetitive punctuation like '..', '..!', '???', excessive underscores/hyphens, and control chars).
    /// </summary>
    public static string RemoveUnwantedCharacters(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // 1. Remove emojis
        var text = RemoveEmojis(input);

        // 2. Normalize fullwidth symbols that are often used in video titles to safe spaces or dashes
        text = text
            .Replace('？', ' ')
            .Replace('：', ' ')
            .Replace('／', '-')
            .Replace('＼', '-')
            .Replace('＊', ' ')
            .Replace('｜', '-')
            .Replace('＂', ' ')
            .Replace('＜', ' ')
            .Replace('＞', ' ')
            .Replace('—', '-')
            .Replace('–', '-');

        // 3. Remove filesystem invalid chars and control chars
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (!invalid.Contains(c) && !ProblematicFileNameChars.Contains(c) && !char.IsControl(c))
            {
                sb.Append(c);
            }
        }
        text = sb.ToString();

        // 4. Collapse repetitive punctuation and clutter
        // Mixed dot-exclamation clutter like .! or ..! -> single !
        text = Regex.Replace(text, @"\.+\s*!+", "!");
        // Multiple dots (.. or ...) -> space
        text = Regex.Replace(text, @"\.{2,}", " ");
        // Multiple exclamation marks -> single !
        text = Regex.Replace(text, @"!{2,}", "!");
        // Space before punctuation like "Movie !" -> "Movie!"
        text = Regex.Replace(text, @"\s+([!])", "$1");
        // Multiple dashes/underscores -> single
        text = Regex.Replace(text, @"-{2,}", "-");
        text = Regex.Replace(text, @"_{2,}", "_");
        // Multiple whitespace characters -> single space
        text = Regex.Replace(text, @"\s+", " ");

        // 5. Trim leading and trailing punctuation and spaces
        var charsToTrim = new[] { ' ', '.', '-', '_', '!', ',', ';', ':', '~', '#' };
        return text.Trim(charsToTrim);
    }

    /// <summary>
    /// Cleans and trims a title to fit within both character and UTF-8 byte limits,
    /// without cutting words or multi-byte characters in half.
    /// </summary>
    public static string TruncateTitle(string? input, int maxChars = DefaultMaxStemChars, int maxBytes = DefaultMaxStemBytes)
    {
        if (string.IsNullOrWhiteSpace(input)) return "video";
        var cleaned = RemoveUnwantedCharacters(input);
        if (string.IsNullOrWhiteSpace(cleaned)) return "video";

        var enumerator = StringInfo.GetTextElementEnumerator(cleaned);
        var sb = new StringBuilder();
        int charCount = 0;
        int byteCount = 0;

        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            var elementBytes = Encoding.UTF8.GetByteCount(element);

            if (charCount >= maxChars || (byteCount + elementBytes > maxBytes))
            {
                break;
            }

            sb.Append(element);
            charCount++;
            byteCount += elementBytes;
        }

        var result = sb.ToString().Trim().TrimEnd('.', '-', '_', ' ', '!');
        return string.IsNullOrWhiteSpace(result) ? "video" : result;
    }

    /// <summary>
    /// Truncates a string strictly to fit within a specified maximum UTF-8 byte length without breaking
    /// multi-byte characters, surrogate pairs, or unicode combining sequences (grapheme clusters).
    /// </summary>
    public static string TruncateByUtf8Bytes(string? input, int maxBytes = DefaultMaxStemBytes, string fallback = "media")
    {
        if (string.IsNullOrWhiteSpace(input)) return fallback;
        var trimmed = RemoveUnwantedCharacters(input);
        if (string.IsNullOrWhiteSpace(trimmed)) return fallback;

        if (Encoding.UTF8.GetByteCount(trimmed) <= maxBytes)
        {
            var clean = trimmed.TrimEnd('.', ' ', '-', '_');
            return string.IsNullOrWhiteSpace(clean) ? fallback : clean;
        }

        var enumerator = StringInfo.GetTextElementEnumerator(trimmed);
        var sb = new StringBuilder();
        int currentBytes = 0;

        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            var elementBytes = Encoding.UTF8.GetByteCount(element);
            if (currentBytes + elementBytes > maxBytes)
            {
                break;
            }
            sb.Append(element);
            currentBytes += elementBytes;
        }

        var result = sb.ToString().Trim().TrimEnd('.', ' ', '-', '_');
        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }

    /// <summary>
    /// Sanitizes a file name stem, removing emojis, unwanted characters, and trimming to fit safely
    /// within filesystem component limits (both character count and UTF-8 bytes).
    /// </summary>
    public static string SanitizeFileName(string? name, int maxStemBytes = DefaultMaxStemBytes, string fallback = "video")
    {
        if (string.IsNullOrWhiteSpace(name)) return fallback;
        return TruncateTitle(name, maxChars: DefaultMaxStemChars, maxBytes: maxStemBytes);
    }

    /// <summary>
    /// Sanitizes a destination file path, trimming the title stem appropriately if either the title
    /// or the full destination path is very long.
    /// </summary>
    public static string SanitizeDestinationPath(
        string targetFolder,
        string rawTitle,
        string extension,
        int maxTotalPathLength = DefaultMaxPathLength,
        int maxStemBytes = DefaultMaxStemBytes)
    {
        var ext = string.IsNullOrWhiteSpace(extension) ? ".mp4" : (extension.StartsWith('.') ? extension : $".{extension}");
        var cleanFolder = targetFolder.TrimEnd('/', '\\');

        // Total path budget: folder + separator + stem + uniqueness margin (12 chars) + ext <= maxTotalPathLength
        var totalFolderChars = cleanFolder.Length + 1;
        var extAndMarginChars = ext.Length + 12;
        var availableChars = Math.Max(20, maxTotalPathLength - totalFolderChars - extAndMarginChars);

        var maxChars = Math.Min(DefaultMaxStemChars, availableChars);
        var safeStem = TruncateTitle(rawTitle, maxChars: maxChars, maxBytes: maxStemBytes);

        var fileName = $"{safeStem}{ext}";
        return Path.Combine(cleanFolder, fileName);
    }

    /// <summary>
    /// Sanitizes a category or subfolder name, removing emojis/unwanted characters, preventing directory traversal,
    /// and enforcing safe length limits.
    /// </summary>
    public static string SanitizeCategoryName(string? rawCategory, int maxBytes = DefaultMaxDirectoryBytes, string fallback = "Downloads")
    {
        if (string.IsNullOrWhiteSpace(rawCategory)) return fallback;

        var clean = RemoveUnwantedCharacters(rawCategory);
        if (string.IsNullOrWhiteSpace(clean) || clean.Contains("..")) return fallback;

        return TruncateTitle(clean, maxChars: 50, maxBytes: maxBytes);
    }

    /// <summary>
    /// Ensures that the specified file path is unique on disk by appending a counter or GUID,
    /// while guaranteeing that the final filename component never exceeds safe UTF-8 byte limits.
    /// </summary>
    public static string EnsureUniqueFilePath(string filePath, int maxStemBytes = DefaultMaxStemBytes)
    {
        var dir = Path.GetDirectoryName(filePath);
        var originalStem = Path.GetFileNameWithoutExtension(filePath);
        var ext = Path.GetExtension(filePath);

        var stem = TruncateTitle(originalStem, maxChars: DefaultMaxStemChars, maxBytes: maxStemBytes);
        var candidatePath = string.IsNullOrEmpty(dir) ? $"{stem}{ext}" : Path.Combine(dir, $"{stem}{ext}");

        if (!File.Exists(candidatePath))
        {
            return candidatePath;
        }

        int counter = 1;
        while (counter < 1000)
        {
            var suffix = $" ({counter})";
            var budget = Math.Max(20, maxStemBytes - Encoding.UTF8.GetByteCount(suffix));
            var candidateStem = TruncateTitle(stem, maxChars: Math.Max(10, DefaultMaxStemChars - suffix.Length), maxBytes: budget);
            var candidateName = $"{candidateStem}{suffix}{ext}";
            var fullCandidate = string.IsNullOrEmpty(dir) ? candidateName : Path.Combine(dir, candidateName);

            if (!File.Exists(fullCandidate))
            {
                return fullCandidate;
            }
            counter++;
        }

        var guidSuffix = $"_{Guid.NewGuid():N}";
        var guidBudget = Math.Max(20, maxStemBytes - Encoding.UTF8.GetByteCount(guidSuffix));
        var fallbackStem = TruncateTitle(stem, maxChars: Math.Max(10, DefaultMaxStemChars - guidSuffix.Length), maxBytes: guidBudget);
        var fallbackName = $"{fallbackStem}{guidSuffix}{ext}";
        return string.IsNullOrEmpty(dir) ? fallbackName : Path.Combine(dir, fallbackName);
    }
}
