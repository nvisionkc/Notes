namespace Notes.Services;

public interface IDiffService
{
    DiffResult ComputeDiff(string left, string right);
}

public class DiffResult
{
    public List<DiffLine> Lines { get; set; } = new();
    public int LinesAdded { get; set; }
    public int LinesRemoved { get; set; }
    public int LinesUnchanged { get; set; }
    public bool HasDifferences => LinesAdded > 0 || LinesRemoved > 0;
}

public class DiffLine
{
    public DiffLineType Type { get; set; }
    public string? LeftContent { get; set; }
    public string? RightContent { get; set; }
    public int? LeftLineNumber { get; set; }
    public int? RightLineNumber { get; set; }
}

public enum DiffLineType
{
    Unchanged,
    Added,
    Removed
}
