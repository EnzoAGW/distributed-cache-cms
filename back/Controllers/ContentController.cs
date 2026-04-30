using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication2.Dtos;
using WebApplication2.Models;
using WebApplication2.Services;

namespace WebApplication2.Controllers;

[ApiController]
[Route("api/content")]
public sealed class ContentController : ControllerBase
{
    private readonly IContentService _contentService;
    private readonly ILogger<ContentController> _logger;

    public ContentController(IContentService contentService, ILogger<ContentController> logger)
    {
        _contentService = contentService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ContentResponse>>> ListAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? tag = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _contentService.ListAsync(new ContentListQuery(page, pageSize, tag, search), cancellationToken);
        Response.Headers.CacheControl = "public,max-age=15";

        return Ok(new PagedResult<ContentResponse>(
            result.Items.Select(ToResponse).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContentResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await _contentService.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        var etag = BuildEtag(item);
        if (Request.Headers.IfNoneMatch == etag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "public,max-age=30";
        return Ok(ToResponse(item));
    }

    [HttpGet("slug/{slug}")]
    public async Task<ActionResult<ContentResponse>> GetBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var item = await _contentService.GetBySlugAsync(slug, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        var etag = BuildEtag(item);
        if (Request.Headers.IfNoneMatch == etag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "public,max-age=30";
        return Ok(ToResponse(item));
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ContentResponse>> CreateAsync(
        [FromBody] CreateContentRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _contentService.CreateAsync(
            request.Slug,
            request.Title,
            request.Body,
            request.Tags,
            cancellationToken);

        _logger.LogInformation("Content created: {Id} (slug: {Slug})", created.Id, created.Slug);
        return CreatedAtAction(nameof(GetByIdAsync), new { id = created.Id }, ToResponse(created));
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ContentResponse>> UpdateAsync(
        Guid id,
        [FromBody] UpdateContentRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _contentService.UpdateAsync(
            id,
            request.Slug,
            request.Title,
            request.Body,
            request.Tags,
            request.ExpectedVersion,
            cancellationToken);

        if (updated is null)
        {
            return NotFound();
        }

        _logger.LogInformation("Content updated: {Id} (slug: {Slug})", updated.Id, updated.Slug);
        return Ok(ToResponse(updated));
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _contentService.DeleteAsync(id, cancellationToken);
        if (deleted)
        {
            _logger.LogInformation("Content deleted: {Id}", id);
        }

        return deleted ? NoContent() : NotFound();
    }

    private static ContentResponse ToResponse(ContentItem item)
    {
        return new ContentResponse(
            item.Id,
            item.Slug,
            item.Title,
            item.Body,
            item.Tags,
            item.CreatedAtUtc,
            item.UpdatedAtUtc,
            item.Version);
    }

    private static string BuildEtag(ContentItem item)
    {
        return $"\"{item.Version}-{item.UpdatedAtUtc.ToUnixTimeMilliseconds()}\"";
    }
}
