namespace DataHub.Core.Entities;

/// <summary>
/// Base class for all auditable domain entities. Provides identity, soft-delete flag,
/// creation / modification audit fields, and a free-form <see cref="Source"/> tag for
/// data provenance (e.g., "manual", "seed", "mlb-api", "csv-import:teams.csv").
/// Audit fields are auto-stamped by <c>DataHubDbContext.SaveChangesAsync</c> using
/// the current HTTP user's email (or "system" outside an HTTP context).
/// </summary>
public abstract class AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Soft-delete / availability flag. <c>false</c> hides the row from normal queries but keeps it for history.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Free-form provenance tag for the row. Examples: "manual", "seed",
    /// "mlb-api", "csv-import:teams-2026.csv", "scrape:espn". Useful for filtering,
    /// re-ingestion, and debugging.
    /// </summary>
    public string? Source { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
}
