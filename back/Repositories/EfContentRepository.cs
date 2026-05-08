using Microsoft.EntityFrameworkCore;
using WebApplication2.Exceptions;
using WebApplication2.Models;
using WebApplication2.Persistence;

namespace WebApplication2.Repositories;

public sealed class EfContentRepository(AppDbContext db) : IContentRepository
{
    public async Task<ContentItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => await db.ContentItems.FindAsync([id], cancellationToken);

    public async Task<ContentItem?> GetBySlugAsync(string slug, CancellationToken cancellationToken)
        => await db.ContentItems.FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);

    public async Task<PagedResult<ContentItem>> ListAsync(ContentListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        IQueryable<ContentItem> data = db.ContentItems;

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var tag = query.Tag.Trim().ToLowerInvariant();
            data = data.Where(x => x.Tags.Contains(tag));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            data = data.Where(x =>
                EF.Functions.ILike(x.Title, $"%{search}%") ||
                EF.Functions.ILike(x.Body, $"%{search}%"));
        }

        var totalCount = await data.CountAsync(cancellationToken);
        var items = await data
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<ContentItem>(items, page, pageSize, totalCount);
    }

    public async Task<ContentItem> CreateAsync(ContentItem item, CancellationToken cancellationToken)
    {
        db.ContentItems.Add(item);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            db.Entry(item).State = EntityState.Detached;
            throw new InvalidOperationException("Slug already exists.");
        }
        return item;
    }

    public async Task<ContentItem?> UpdateAsync(ContentItem item, long? expectedVersion, CancellationToken cancellationToken)
    {
        var current = await db.ContentItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == item.Id, cancellationToken);

        if (current is null) return null;

        if (expectedVersion.HasValue && current.Version != expectedVersion.Value)
            throw new ConcurrencyConflictException(expectedVersion.Value, current.Version);

        if (!current.Slug.Equals(item.Slug, StringComparison.OrdinalIgnoreCase))
        {
            var slugTaken = await db.ContentItems
                .AnyAsync(x => x.Slug == item.Slug && x.Id != item.Id, cancellationToken);
            if (slugTaken)
                throw new InvalidOperationException("Slug already exists.");
        }

        db.ContentItems.Update(item);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            db.Entry(item).State = EntityState.Detached;
            throw new InvalidOperationException("Slug already exists.");
        }
        return item;
    }

    public async Task<ContentItem?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.ContentItems.FindAsync([id], cancellationToken);
        if (item is null) return null;

        db.ContentItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505") == true
           || ex.InnerException?.Message.Contains("unique") == true;
}
