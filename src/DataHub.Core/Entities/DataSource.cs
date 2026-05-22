namespace DataHub.Core.Entities;

public class DataSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Free-form JSON config for the source (API keys ref, schedule, endpoints, etc).</summary>
    public string? ConfigJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DataEntry> DataEntries { get; set; } = new List<DataEntry>();
}
