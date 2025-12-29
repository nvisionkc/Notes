namespace Notes.Data.Entities;

public class Script
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// C# source code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of what the script does
    /// </summary>
    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft delete support
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Last execution time for sorting by recently used
    /// </summary>
    public DateTime? LastRunAt { get; set; }

    /// <summary>
    /// Number of times executed
    /// </summary>
    public int RunCount { get; set; } = 0;
}
