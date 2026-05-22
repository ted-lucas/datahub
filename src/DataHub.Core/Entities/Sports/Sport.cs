namespace DataHub.Core.Entities.Sports;

/// <summary>Top of the sports taxonomy. Examples: Baseball, Football, Basketball, Hockey, Golf.</summary>
public class Sport : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? IconRef { get; set; }
    public int SortOrder { get; set; }

    public ICollection<SportLevel> Levels { get; set; } = new List<SportLevel>();
}
