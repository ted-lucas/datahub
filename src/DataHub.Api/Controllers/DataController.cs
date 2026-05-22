using System.Security.Claims;
using DataHub.Core.Constants;
using DataHub.Core.DTOs.Data;
using DataHub.Core.Entities;
using DataHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataHub.Api.Controllers;

[ApiController]
[Route("api/data-sources")]
[Authorize]
public class DataSourcesController : ControllerBase
{
    private readonly DataHubDbContext _db;
    public DataSourcesController(DataHubDbContext db) => _db = db;

    [HttpGet]
    [Authorize(Policy = Permissions.DataRead)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _db.DataSources.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.DataRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var src = await _db.DataSources.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        return src is null ? NotFound() : Ok(src);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.SourcesManage)]
    public async Task<IActionResult> Create([FromBody] CreateDataSourceRequest request, CancellationToken ct)
    {
        var src = new DataSource
        {
            Name = request.Name,
            Type = request.Type,
            Description = request.Description,
            ConfigJson = request.ConfigJson,
        };
        _db.DataSources.Add(src);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = src.Id }, src);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.SourcesManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDataSourceRequest request, CancellationToken ct)
    {
        var src = await _db.DataSources.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (src is null) return NotFound();
        src.Name = request.Name;
        src.Type = request.Type;
        src.Description = request.Description;
        src.ConfigJson = request.ConfigJson;
        await _db.SaveChangesAsync(ct);
        return Ok(src);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.SourcesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var src = await _db.DataSources.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (src is null) return NotFound();
        _db.DataSources.Remove(src);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/data-entries")]
[Authorize]
public class DataEntriesController : ControllerBase
{
    private readonly DataHubDbContext _db;
    public DataEntriesController(DataHubDbContext db) => _db = db;

    [HttpGet]
    [Authorize(Policy = Permissions.DataRead)]
    public async Task<IActionResult> Query(
        [FromQuery] Guid? sourceId,
        [FromQuery] string? category,
        [FromQuery] string? tag,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        var q = _db.DataEntries.AsNoTracking().AsQueryable();
        if (sourceId.HasValue) q = q.Where(e => e.DataSourceId == sourceId);
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(e => e.Category == category);
        if (!string.IsNullOrWhiteSpace(tag)) q = q.Where(e => e.Tags != null && e.Tags.Contains(tag));
        q = q.OrderByDescending(e => e.CreatedAt).Skip(skip).Take(Math.Min(take, 500));
        return Ok(await q.ToListAsync(ct));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.DataRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var entry = await _db.DataEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        return entry is null ? NotFound() : Ok(entry);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.DataWrite)]
    public async Task<IActionResult> Create([FromBody] CreateDataEntryRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        Guid? userId = Guid.TryParse(userIdClaim, out var uid) ? uid : null;

        var entry = new DataEntry
        {
            DataSourceId = request.DataSourceId,
            Category = request.Category,
            Tags = request.Tags,
            PayloadJson = request.PayloadJson,
            CreatedByUserId = userId,
        };
        _db.DataEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = entry.Id }, entry);
    }
}
