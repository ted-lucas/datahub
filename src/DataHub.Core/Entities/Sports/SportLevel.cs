namespace DataHub.Core.Entities.Sports;

/// <summary>A level within a Sport. Examples: Professional, Collegiate, High School.</summary>
public class SportLevel : AuditableEntity
{
    public Guid SportId { get; set; }
    public Sport? Sport { get; set; }

    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public ICollection<League> Leagues { get; set; } = new List<League>();
}
