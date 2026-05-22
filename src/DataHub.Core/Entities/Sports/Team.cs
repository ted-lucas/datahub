namespace DataHub.Core.Entities.Sports;

/// <summary>A team within a League. Disambiguated by League (e.g. multiple "Cardinals" exist).</summary>
public class Team : AuditableEntity
{
    public Guid LeagueId { get; set; }
    public League? League { get; set; }

    public Guid? ConferenceId { get; set; }
    public Conference? Conference { get; set; }

    public Guid? VenueId { get; set; }
    public Venue? Venue { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public int? FoundedYear { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? LogoRef { get; set; }

    public ICollection<TeamSeason> Seasons { get; set; } = new List<TeamSeason>();
}
