namespace Notes.Models.AI;

public class ChatResponse
{
    public bool Success { get; set; }
    public string? Content { get; set; }
    public string? ErrorMessage { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public string? ModelUsed { get; set; }
}
