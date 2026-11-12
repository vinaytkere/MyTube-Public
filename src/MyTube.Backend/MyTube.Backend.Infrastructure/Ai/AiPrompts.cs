namespace MyTube.Backend.Infrastructure.Ai;

public static class AiPrompts
{
    public static string BuildMetadataSystemPrompt(string existingCategories)
    {
        return $@"You are an AI video media analyzer for MyTube. Given a video's file name and transcript, generate a structured JSON object.
Return ONLY a valid JSON object matching this exact schema:
{{
  ""suggestedTitle"": ""A clear, clean, professional title without file extensions or gibberish"",
  ""summary"": ""A concise 2-3 sentence summary of the video content"",
  ""tags"": [""tag1"", ""tag2"", ""tag3"", ""tag4"", ""tag5""],
  ""detectedLanguage"": ""Language name e.g. English, Hindi, Spanish"",
  ""suggestedCategory"": ""Single category name e.g. Education, Technology, Gaming, Music, Podcast, Movies, News""
}}

Existing categories in library: {existingCategories}. If suitable, reuse an existing category name or suggest a standard clean category name.";
    }

    public static string BuildMetadataUserPrompt(string originalFileName, string transcript)
    {
        return $"Original File Name: {originalFileName}\nTranscript: {transcript}";
    }

    public static string BuildQaSystemPrompt()
    {
        return @"You are an expert AI Video & Web Content Assistant. Synthesize and format a clean, concise, elegant Markdown response for the user.
Requirements:
1. Provide a direct, well-written answer to the question using bullet points and short paragraphs.
2. Filter out web clutter: DO NOT include raw tracking redirect links, raw YouTube thumbnail images, or duplicate text strings.
3. If relevant sources are mentioned, format them neatly as clean markdown links [Source Name](url).
4. Ensure the output is well-structured with clear subheadings (e.g. ### Key Insights, ### Web Context & Analysis, ### Summary).
5. Never output raw HTML or JSON code blocks. Return clean, formatted Markdown prose only.";
    }

    public static string BuildQaUserPrompt(string videoTitle, string videoSummary, string transcriptExcerpt, string question, string webContext)
    {
        var transcriptPart = string.IsNullOrWhiteSpace(transcriptExcerpt) ? "" : $"\nVideo Transcript: {transcriptExcerpt}";
        var webPart = string.IsNullOrWhiteSpace(webContext) ? "No web search context available." : webContext;

        return $"Video Title: {videoTitle}\nVideo Summary: {videoSummary}{transcriptPart}\nUser Question: {question}\n\nWeb Search Information:\n{webPart}";
    }
}
