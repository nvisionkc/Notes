namespace Notes.Models.AI;

public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsStreaming { get; set; }
}

public enum ChatRole
{
    User,
    Assistant
}
