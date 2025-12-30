namespace Notes.Services;

public interface ICommandService
{
    void RegisterCommand(Command command);
    void UnregisterCommand(string commandId);
    void ClearCategory(string category);
    List<Command> Search(string query, int limit = 15);
    List<Command> GetAll();
    List<Command> GetByCategory(string category);
    Task ExecuteAsync(string commandId);
    List<Command> GetRecent(int limit = 5);
    void RequestOpenPalette();
    event Action? CommandsChanged;
    event Action? OpenPaletteRequested;
}

public class Command
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Category { get; set; } = "";  // "Scripts", "Notes", "Actions", "Navigate"
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public Func<Task>? Action { get; set; }
    public string? Keywords { get; set; }  // Additional search terms
}
