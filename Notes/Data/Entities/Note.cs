namespace Notes.Data.Entities;

public class Note
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Stores HTML content from the rich text editor
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Plain text excerpt for sidebar preview
    /// </summary>
    public string Preview { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft delete support
    /// </summary>
    public bool IsDeleted { get; set; } = false;
}
