using Notes.Data.Entities;

namespace Notes.Services;

public interface INoteService
{
    Task<Note> CreateNoteAsync();
    Task<Note?> GetNoteAsync(int id);
    Task<List<Note>> GetAllNotesAsync();
    Task SaveNoteAsync(Note note);
    Task DeleteNoteAsync(int id);
}
