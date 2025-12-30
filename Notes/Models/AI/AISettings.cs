namespace Notes.Models.AI;

public class AISettings
{
    public string Model { get; set; } = ClaudeModels.Sonnet;
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 4096;
    public string SystemPrompt { get; set; } = DefaultSystemPrompt;
    public bool StreamResponses { get; set; } = true;

    public const string DefaultSystemPrompt = """
        You are an AI assistant integrated into Motes, a developer utility app. You help users with:

        - Writing and editing notes
        - Creating C# scripts using the built-in scripting engine
        - Understanding encoding/decoding operations
        - Analyzing JSON/XML data
        - Comparing text differences

        When writing scripts, use the available globals and helper methods. Be concise and provide working code.
        """;
}

public static class ClaudeModels
{
    public const string Haiku = "claude-3-haiku-20240307";
    public const string Sonnet = "claude-sonnet-4-20250514";
    public const string Opus = "claude-opus-4-20250514";

    public static string GetDisplayName(string model) => model switch
    {
        Haiku => "Claude 3 Haiku (Fast)",
        Sonnet => "Claude Sonnet 4 (Balanced)",
        Opus => "Claude Opus 4 (Most Capable)",
        _ => model
    };

    public static IReadOnlyList<string> All => [Haiku, Sonnet, Opus];
}
