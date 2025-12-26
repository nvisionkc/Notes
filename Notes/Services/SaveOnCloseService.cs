namespace Notes.Services;

/// <summary>
/// Service to coordinate saving notes when the app is closing.
/// </summary>
public class SaveOnCloseService
{
    public event Func<Task>? SaveRequested;

    public async Task RequestSaveAsync()
    {
        if (SaveRequested != null)
        {
            await SaveRequested.Invoke();
        }
    }
}
