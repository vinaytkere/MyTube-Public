using System;

namespace MyTube.Backend.Application.AI.Models;

public enum SubtitleLanguage
{
    Original = 0,
    English = 1,
    Kannada = 2,
    Hindi = 3,
    Spanish = 4
}

public static class SubtitleLanguageExtensions
{
    public static string ToLanguageCode(this SubtitleLanguage language)
    {
        return language switch
        {
            SubtitleLanguage.English => "en",
            SubtitleLanguage.Kannada => "kn",
            SubtitleLanguage.Hindi => "hi",
            SubtitleLanguage.Spanish => "es",
            _ => "auto"
        };
    }

    public static string ToProviderLanguageCode(this SubtitleLanguage language)
    {
        return language switch
        {
            SubtitleLanguage.English => "en-US",
            SubtitleLanguage.Kannada => "kn-IN",
            SubtitleLanguage.Hindi => "hi-IN",
            SubtitleLanguage.Spanish => "es-ES",
            _ => "auto"
        };
    }

    public static SubtitleLanguage ParseLanguage(string? langStr)
    {
        if (string.IsNullOrWhiteSpace(langStr)) return SubtitleLanguage.Original;
        var clean = langStr.Trim().ToLowerInvariant();

        return clean switch
        {
            "en" or "en-us" or "en-in" or "english" => SubtitleLanguage.English,
            "kn" or "kn-in" or "kannada" => SubtitleLanguage.Kannada,
            "hi" or "hi-in" or "hindi" => SubtitleLanguage.Hindi,
            "es" or "es-es" or "spanish" => SubtitleLanguage.Spanish,
            _ => SubtitleLanguage.Original
        };
    }

    public static bool IsSameLanguage(SubtitleLanguage lang1, SubtitleLanguage lang2)
    {
        if (lang1 == SubtitleLanguage.Original || lang2 == SubtitleLanguage.Original)
            return true;

        return lang1 == lang2;
    }
}
