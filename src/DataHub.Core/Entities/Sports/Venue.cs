namespace DataHub.Core.Entities.Sports;

/// <summary>A physical stadium / arena / venue. Geolocated to support map overlays.</summary>
public class Venue : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public double? Lat { get; set; }
    public double? Lon { get; set; }
    public int? Capacity { get; set; }
    public int? OpenedYear { get; set; }
    public int? ClosedYear { get; set; }

    public ICollection<Team> Teams { get; set; } = new List<Team>();
}
