namespace DataHub.Core.Entities.Sports;

/// <summary>A league or top-level organization within a SportLevel. Examples: MLB, NFL, NCAA D-I.</summary>
public class League : AuditableEntity
{
    public Guid SportLevelId { get; set; }
    public SportLevel? SportLevel { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Abbreviation { get; set; }
    public string? Country { get; set; }
    public int? FoundedYear { get; set; }

    public ICollection<Conference> Conferences { get; set; } = new List<Conference>();
    public ICollection<Team> Teams { get; set; } = new List<Team>();
}
