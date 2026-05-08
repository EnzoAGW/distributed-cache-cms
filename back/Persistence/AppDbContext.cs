using Microsoft.EntityFrameworkCore;
using WebApplication2.Models;

namespace WebApplication2.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ContentItem> ContentItems => Set<ContentItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContentItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Body).HasColumnType("text").IsRequired();
            e.Property(x => x.Tags).HasColumnType("text[]").IsRequired();
            e.Property(x => x.CreatedAtUtc).IsRequired();
            e.Property(x => x.UpdatedAtUtc).IsRequired();
            e.Property(x => x.Version).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
        });
    }
}
