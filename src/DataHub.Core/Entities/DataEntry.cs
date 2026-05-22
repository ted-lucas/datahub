namespace DataHub.Core.Entities;

public class DataEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? DataSourceId { get; set; }
    public DataSource? DataSource { get; set; }

    public string Category { get; set; } = string.Empty;
    /// <summary>Comma-separated tags for simple filtering; can evolve to a Tag table later.</summary>
    public string? Tags { get; set; }
    /// <summary>The actual payload as JSON. Flexible schema for any data domain.</summary>
    public string PayloadJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}
