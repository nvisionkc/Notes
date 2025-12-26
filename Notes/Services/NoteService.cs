using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Notes.Data;
using Notes.Data.Entities;

namespace Notes.Services;

public partial class NoteService : INoteService
{
    private readonly IDbContextFactory<NotesDbContext> _contextFactory;

    public NoteService(IDbContextFactory<NotesDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public Task<Note> CreateNoteAsync()
    {
        return Task.FromResult(new Note
        {
            Title = "Untitled Note",
            Content = string.Empty,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        });
    }

    public async Task<Note?> GetNoteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Notes.FindAsync(id);
    }

    public async Task<List<Note>> GetAllNotesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Notes
            .OrderByDescending(n => n.ModifiedAt)
            .ToListAsync();
    }

    public async Task SaveNoteAsync(Note note)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        note.ModifiedAt = DateTime.UtcNow;
        note.Preview = GeneratePreview(note.Content);
        note.Title = ExtractTitle(note.Content) ?? "Untitled Note";

        if (note.Id == 0)
        {
            context.Notes.Add(note);
        }
        else
        {
            context.Notes.Update(note);
        }

        await context.SaveChangesAsync();
    }

    public async Task DeleteNoteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var note = await context.Notes.FindAsync(id);
        if (note != null)
        {
            note.IsDeleted = true;
            await context.SaveChangesAsync();
        }
    }

    private static string GeneratePreview(string htmlContent)
    {
        if (string.IsNullOrEmpty(htmlContent)) return string.Empty;

        var text = StripHtmlRegex().Replace(htmlContent, " ");
        text = WhitespaceRegex().Replace(text, " ").Trim();
        return text.Length > 150 ? text[..150] + "..." : text;
    }

    private static string? ExtractTitle(string htmlContent)
    {
        if (string.IsNullOrEmpty(htmlContent)) return null;

        var text = StripHtmlRegex().Replace(htmlContent, " ");
        text = WhitespaceRegex().Replace(text, " ").Trim();

        var firstLine = text.Split('\n').FirstOrDefault()?.Trim();
        if (string.IsNullOrEmpty(firstLine)) return null;

        return firstLine.Length > 50 ? firstLine[..50] + "..." : firstLine;
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex StripHtmlRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
