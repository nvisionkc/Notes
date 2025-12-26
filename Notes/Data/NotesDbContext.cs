using Microsoft.EntityFrameworkCore;
using Notes.Data.Entities;

namespace Notes.Data;

public class NotesDbContext : DbContext
{
    public DbSet<Note> Notes => Set<Note>();

    public NotesDbContext(DbContextOptions<NotesDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.Preview).HasMaxLength(200);
            entity.HasIndex(e => e.ModifiedAt);
            entity.HasIndex(e => e.IsDeleted);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });
    }
}
