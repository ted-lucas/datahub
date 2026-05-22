namespace DataHub.Core.Entities;

public class DataEntry : AuditableEntity
{
    public Guid? DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }

    public string Category { get; set; } = string.Empty;
    /// <summary>Comma-separated tags for simple filtering; can evolve to a Tag table later.</summary>
    public string? Tags { get; set; }
    /// <summary>The actual payload as JSON. Flexible schema for any data domain.</summary>
    public string PayloadJson { get; set; } = "{}";

    /// <summary>
    /// Domain-specific attribution of the entry to a user (not the same as audit <see cref="AuditableEntity.CreatedBy"/>,
    /// which is the email stamp of whoever wrote the row).
    /// </summary>
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}
